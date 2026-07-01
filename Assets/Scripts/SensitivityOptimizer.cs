using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// SensitivityOptimizer.cs
// Uses Bayesian Optimization with a Gaussian Process to figure out the best sensitivity.
// I learned about GP regression from a YouTube video + some Wikipedia reading.
// Basic idea: split the sensitivity range into bins, score each shot, fit a curve over
// the scores using a GP, then use Expected Improvement to pick which sensitivity to try next.
// No external libraries needed - all the math is done manually in C#.

public class SensitivityOptimizer : MonoBehaviour
{
    public static SensitivityOptimizer Instance { get; private set; }

    [Header("Sensitivity Range")]
    [SerializeField] private float minSens = 0.05f;
    [SerializeField] private float maxSens = 0.40f;
    // number of buckets to split the range into
    [SerializeField] private int numBins = 30;

    [Header("Gaussian Process Settings")]
    // controls how smooth the curve is - tweak this if results look weird
    [SerializeField] private float lengthScale = 0.04f;
    [SerializeField] private float signalVariance = 1.0f;
    // how noisy individual shots are - higher = more uncertainty per shot
    [SerializeField] private float noiseVariance = 0.15f;

    [Header("Shot Quality Weights")]
    // these three should add up to 1.0 ideally
    [SerializeField] [Range(0f, 1f)] private float wPrecision = 0.50f;
    [SerializeField] [Range(0f, 1f)] private float wSpeed = 0.30f;
    [SerializeField] [Range(0f, 1f)] private float wDifficulty = 0.20f;

    [Header("Learning")]
    // older data counts less each session so we don't get stuck on old results
    [SerializeField] private float sessionDecay = 0.95f;
    [SerializeField] private int minShotsNeeded = 10;
    [SerializeField] private int minBinsForConvergence = 8;
    [SerializeField] private float convergenceVarThreshold = 0.15f;
    // smoothing factor for the live display (exponential moving average)
    [SerializeField] private float ewmaAlpha = 0.15f;

    // internal state
    private BinState[] bins;
    private float[] posteriorMean;
    private float[] posteriorVar;
    private bool dirty = true; // flag to recompute GP when data changes
    private int totalShots;
    private bool saved;

    // prior mean - using 0.5 as a neutral starting point
    private const float PRIOR_MEAN = 0.5f;
    private const double JITTER = 1e-6; // small value to keep the matrix positive definite

    // public getters
    public float MinSens => minSens;
    public float MaxSens => maxSens;
    public int NumBins => numBins;
    public int TotalShots => totalShots;

    public float GetBinSensitivity(int i) { return bins != null && i >= 0 && i < numBins ? bins[i].center : 0f; }
    public float GetPosteriorMean(int i) { UpdateGP(); return posteriorMean != null && i >= 0 && i < numBins ? posteriorMean[i] : PRIOR_MEAN; }
    public float GetPosteriorVariance(int i) { UpdateGP(); return posteriorVar != null && i >= 0 && i < numBins ? posteriorVar[i] : signalVariance; }
    public int GetBinShotCount(int i) { return bins != null && i >= 0 && i < numBins ? bins[i].rawShotCount : 0; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "senzai_optimizer.json");

    // classes used for save/load
    [Serializable]
    public class OptimizerSaveData
    {
        public int version;
        public int totalShots;
        public OptimizerBinEntry[] bins;
    }

    [Serializable]
    public class OptimizerBinEntry
    {
        public float center;
        public float qualitySum;
        public float weightSum;
        public int shotCount;
        public float ewma;
    }

    // internal bin - not serialized directly
    private class BinState
    {
        public float center;
        public float qualitySum;
        public float weightSum;
        public int rawShotCount;
        public float ewma;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializeBins();
        LoadModel();
    }

    private void OnApplicationQuit() => SaveModel();
    private void OnDestroy() { if (!saved) SaveModel(); }

    private void InitializeBins()
    {
        bins = new BinState[numBins];
        posteriorMean = new float[numBins];
        posteriorVar = new float[numBins];

        for (int i = 0; i < numBins; i++)
        {
            float t = numBins > 1 ? (float)i / (numBins - 1) : 0.5f;
            bins[i] = new BinState
            {
                center = Mathf.Lerp(minSens, maxSens, t),
                qualitySum = 0f,
                weightSum = 0f,
                rawShotCount = 0,
                ewma = PRIOR_MEAN
            };
            posteriorMean[i] = PRIOR_MEAN;
            posteriorVar[i] = signalVariance;
        }

        dirty = true;
    }

    // gives a quality score from 0 to 1 for a single shot
    public float ComputeShotQuality(bool hit, float aimDeviation, float reactionTime, float distance)
    {
        if (!hit) return 0f;

        // lower deviation = better precision score
        float precision = 1f / (1f + Mathf.Max(0f, aimDeviation) / 3f);

        // faster reaction = better speed score
        float speed = reactionTime >= 0f
            ? Mathf.Exp(-reactionTime * 0.5f)
            : 0.5f; // if we don't have timing data, just use 0.5

        // further targets are harder so reward them more
        float difficulty = distance >= 0f
            ? Mathf.Clamp01(distance / 40f)
            : 0.3f; // default if distance wasn't tracked

        return precision * wPrecision + speed * wSpeed + difficulty * wDifficulty;
    }

    public void RecordShot(bool hit, float sensitivity, float aimDeviation, float reactionTime, float distance)
    {
        // ignore shots outside the tracked range
        if (sensitivity < minSens || sensitivity > maxSens) return;

        float quality = ComputeShotQuality(hit, aimDeviation, reactionTime, distance);

        int idx = SensitivityToBinIndex(sensitivity);

        bins[idx].qualitySum += quality;
        bins[idx].weightSum += 1f;
        bins[idx].rawShotCount += 1;

        // update running average for the real-time display
        bins[idx].ewma = ewmaAlpha * quality + (1f - ewmaAlpha) * bins[idx].ewma;

        totalShots++;
        dirty = true; // mark GP as needing update
    }

    // Gaussian Process update
    // fits a smooth curve over all the bin observations using Cholesky decomposition
    // TODO: could cache the decomposition if it ever becomes a performance issue
    private void UpdateGP()
    {
        if (!dirty) return;
        dirty = false;

        var activeIdx = new List<int>();
        var yObs = new List<double>();
        var noisePerBin = new List<double>();

        // only use bins that actually have data
        for (int i = 0; i < numBins; i++)
        {
            if (bins[i].weightSum >= 0.5f)
            {
                activeIdx.Add(i);
                yObs.Add(bins[i].qualitySum / bins[i].weightSum - PRIOR_MEAN); // subtract prior mean
                noisePerBin.Add(noiseVariance / Math.Max(1.0, bins[i].weightSum) + JITTER);
            }
        }

        int n = activeIdx.Count;

        // no data yet - just use the prior
        if (n == 0)
        {
            for (int i = 0; i < numBins; i++)
            {
                posteriorMean[i] = PRIOR_MEAN;
                posteriorVar[i] = signalVariance;
            }
            return;
        }

        // build kernel matrix K(X, X) with noise on diagonal
        double[,] Ky = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                Ky[i, j] = RBFKernel(bins[activeIdx[i]].center, bins[activeIdx[j]].center);
            Ky[i, i] += noisePerBin[i];
        }

        // Cholesky decompose the kernel matrix
        double[,] L = CholeskyDecompose(Ky, n);
        if (L == null)
        {
            // matrix wasn't positive definite, add a bit more jitter and retry
            for (int i = 0; i < n; i++) Ky[i, i] += 1e-4;
            L = CholeskyDecompose(Ky, n);
        }
        if (L == null)
        {
            // still failed, just fall back to the EWMA running averages
            Debug.LogWarning("[SensitivityOptimizer] Cholesky decomposition failed. Using EWMA fallback.");
            for (int i = 0; i < numBins; i++)
            {
                posteriorMean[i] = bins[i].weightSum > 0f ? bins[i].ewma : PRIOR_MEAN;
                posteriorVar[i] = signalVariance;
            }
            return;
        }

        double[] yArr = yObs.ToArray();
        double[] alpha = CholeskySolve(L, yArr, n);

        // compute posterior mean and variance at every bin
        for (int q = 0; q < numBins; q++)
        {
            float sq = bins[q].center;

            // k* = kernel between this query point and all training points
            double[] kStar = new double[n];
            for (int i = 0; i < n; i++)
                kStar[i] = RBFKernel(sq, bins[activeIdx[i]].center);

            // posterior mean = prior mean + k* . alpha
            double mu = 0.0;
            for (int i = 0; i < n; i++)
                mu += kStar[i] * alpha[i];
            posteriorMean[q] = (float)(mu + PRIOR_MEAN);

            // posterior variance = k(x,x) - v^T v  where v = L^{-1} k*
            double kSelf = RBFKernel(sq, sq);
            double[] v = CholeskySolveLower(L, kStar, n);
            double vTv = 0.0;
            for (int i = 0; i < n; i++)
                vTv += v[i] * v[i];
            posteriorVar[q] = (float)Math.Max(1e-6, kSelf - vTv);
        }
    }

    // RBF kernel - measures similarity between two sensitivity values
    // values close together are more correlated
    private double RBFKernel(float s1, float s2)
    {
        double diff = s1 - s2;
        return signalVariance * Math.Exp(-diff * diff / (2.0 * lengthScale * lengthScale));
    }

    // standard Cholesky decomposition - returns null if the matrix isn't positive definite
    private static double[,] CholeskyDecompose(double[,] A, int n)
    {
        double[,] L = new double[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j <= i; j++)
            {
                double sum = 0.0;
                for (int k = 0; k < j; k++)
                    sum += L[i, k] * L[j, k];

                if (i == j)
                {
                    double val = A[i, i] - sum;
                    if (val <= 0.0) return null; // not positive definite
                    L[i, j] = Math.Sqrt(val);
                }
                else
                {
                    L[i, j] = (A[i, j] - sum) / L[j, j];
                }
            }
        }
        return L;
    }

    // solve L L^T x = b using forward then back substitution
    private static double[] CholeskySolve(double[,] L, double[] b, int n)
    {
        // forward pass
        double[] z = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0.0;
            for (int j = 0; j < i; j++) s += L[i, j] * z[j];
            z[i] = (b[i] - s) / L[i, i];
        }

        // back substitution
        double[] x = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double s = 0.0;
            for (int j = i + 1; j < n; j++) s += L[j, i] * x[j];
            x[i] = (z[i] - s) / L[i, i];
        }

        return x;
    }

    // just the lower triangular solve (needed for variance calculation)
    private static double[] CholeskySolveLower(double[,] L, double[] b, int n)
    {
        double[] z = new double[n];
        for (int i = 0; i < n; i++)
        {
            double s = 0.0;
            for (int j = 0; j < i; j++) s += L[i, j] * z[j];
            z[i] = (b[i] - s) / L[i, i];
        }
        return z;
    }

    // Expected Improvement - how much better could this sensitivity be vs the current best?
    private static float ComputeEI(float mean, float variance, float fBest)
    {
        if (variance < 1e-8f) return 0f;

        float sigma = Mathf.Sqrt(variance);
        float z = (mean - fBest) / sigma;
        float ei = (mean - fBest) * NormalCDF(z) + sigma * NormalPDF(z);
        return Mathf.Max(0f, ei);
    }

    private static float NormalPDF(float x)
    {
        return (float)(Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI));
    }

    // normal CDF approximation using a polynomial - found this in a numerical methods reference
    private static float NormalCDF(float x)
    {
        if (x < -8f) return 0f;
        if (x > 8f) return 1f;

        bool neg = x < 0f;
        if (neg) x = -x;

        float t = 1f / (1f + 0.2316419f * x);
        float d = NormalPDF(x);
        float p = d * t * (0.319381530f
                    + t * (-0.356563782f
                    + t * (1.781477937f
                    + t * (-1.821255978f
                    + t * 1.330274429f))));

        return neg ? p : 1f - p;
    }

    // get the current best sensitivity recommendation
    public SensitivityResult GetRecommendation()
    {
        UpdateGP();

        int binsWithData = 0;
        for (int i = 0; i < numBins; i++)
            if (bins[i].weightSum >= 0.5f) binsWithData++;

        // need more shots first
        if (totalShots < minShotsNeeded)
        {
            return new SensitivityResult
            {
                hasData = false,
                reason = $"Only {totalShots} shots recorded (need {minShotsNeeded}).\n" +
                         "Keep playing to collect more data.",
                shotsSampled = totalShots,
                binsExplored = binsWithData,
                totalBins = numBins
            };
        }

        // find the peak of the GP posterior curve
        int bestIdx = 0;
        float bestMean = float.MinValue;
        for (int i = 0; i < numBins; i++)
        {
            if (posteriorMean[i] > bestMean)
            {
                bestMean = posteriorMean[i];
                bestIdx = i;
            }
        }

        // get sub-bin precision using parabola interpolation
        float optSens = RefineOptimum(bestIdx);

        // confidence interval - sensitivities within ~1.645 std dev of the peak
        // (that's roughly a 90% CI)
        float threshold = Mathf.Sqrt(posteriorVar[bestIdx]) * 1.645f;
        float ciLow = bins[bestIdx].center;
        float ciHigh = bins[bestIdx].center;

        for (int i = bestIdx - 1; i >= 0; i--)
        {
            if (posteriorMean[i] >= bestMean - threshold)
                ciLow = bins[i].center;
            else break;
        }
        for (int i = bestIdx + 1; i < numBins; i++)
        {
            if (posteriorMean[i] >= bestMean - threshold)
                ciHigh = bins[i].center;
            else break;
        }

        // confidence score based on how much of the range we've explored and how many shots
        float dataCoverage = (float)binsWithData / numBins;
        float dataQuantity = Mathf.Clamp01(totalShots / 200f);
        float confidence = dataCoverage * 0.5f + dataQuantity * 0.5f;

        // convergence = how much uncertainty is left in the model
        float maxVar = 0f;
        for (int i = 0; i < numBins; i++)
            maxVar = Mathf.Max(maxVar, posteriorVar[i]);
        float convergence = Mathf.Clamp01(1f - maxVar / signalVariance);

        string reason =
            $"Optimal sensitivity: {optSens:F3}\n" +
            $"90% CI: [{ciLow:F3} - {ciHigh:F3}]\n" +
            $"{totalShots} shots across {binsWithData}/{numBins} bins\n" +
            $"Convergence: {convergence * 100f:F0}%";

        return new SensitivityResult
        {
            hasData = true,
            recommendedSensitivity = optSens,
            confidence = confidence,
            ciLow = ciLow,
            ciHigh = ciHigh,
            convergenceProgress = convergence,
            reason = reason,
            shotsSampled = totalShots,
            sessionsRead = 0,
            binsExplored = binsWithData,
            totalBins = numBins
        };
    }

    // fits a parabola through 3 points around the best bin to find a more precise peak
    private float RefineOptimum(int bestIdx)
    {
        if (bestIdx <= 0 || bestIdx >= numBins - 1)
            return bins[bestIdx].center;

        float h = bins[1].center - bins[0].center; // uniform bin spacing
        float y0 = posteriorMean[bestIdx - 1];
        float y1 = posteriorMean[bestIdx];
        float y2 = posteriorMean[bestIdx + 1];

        // if the second difference is positive it's convex not a peak, so just return center
        float denom = y2 - 2f * y1 + y0;
        if (denom >= 0f) return bins[bestIdx].center;

        // vertex of the fitted parabola
        float offset = -h * (y2 - y0) / (2f * denom);
        return Mathf.Clamp(bins[bestIdx].center + offset, minSens, maxSens);
    }

    // picks the next sensitivity to try using Expected Improvement (the exploration step)
    public float GetExplorationSensitivity()
    {
        UpdateGP();

        // if there's barely any data, pick randomly to start filling in bins
        if (totalShots < 5)
            return UnityEngine.Random.Range(minSens, maxSens);

        // find best observed value so far
        float fBest = float.MinValue;
        for (int i = 0; i < numBins; i++)
        {
            if (bins[i].weightSum >= 0.5f)
                fBest = Mathf.Max(fBest, posteriorMean[i]);
        }
        if (fBest < 0f) fBest = 0f;

        // compute EI at each bin and pick the best one
        float bestEI = float.MinValue;
        int bestIdx = numBins / 2;

        for (int i = 0; i < numBins; i++)
        {
            float ei = ComputeEI(posteriorMean[i], posteriorVar[i], fBest);
            if (ei > bestEI)
            {
                bestEI = ei;
                bestIdx = i;
            }
        }

        // if EI is basically zero everywhere (fully explored or flat), go for the most uncertain bin
        if (bestEI < 1e-6f)
        {
            float maxVar = float.MinValue;
            for (int i = 0; i < numBins; i++)
            {
                if (posteriorVar[i] > maxVar)
                {
                    maxVar = posteriorVar[i];
                    bestIdx = i;
                }
            }
        }

        // add a small random offset so we don't always test the exact same value
        float binWidth = (maxSens - minSens) / Mathf.Max(1f, numBins - 1);
        float sens = bins[bestIdx].center + UnityEngine.Random.Range(-binWidth * 0.3f, binWidth * 0.3f);

        return Mathf.Clamp(sens, minSens, maxSens);
    }

    // true when we think we've gathered enough data and the model has converged
    public bool HasConverged
    {
        get
        {
            if (totalShots < minShotsNeeded) return false;

            UpdateGP();

            int binsWithData = 0;
            float maxVar = 0f;
            for (int i = 0; i < numBins; i++)
            {
                if (bins[i].weightSum >= 0.5f) binsWithData++;
                maxVar = Mathf.Max(maxVar, posteriorVar[i]);
            }

            return binsWithData >= minBinsForConvergence && maxVar < convergenceVarThreshold;
        }
    }

    // save model to disk so data survives between sessions
    public void SaveModel()
    {
        if (saved || bins == null) return;

        var data = new OptimizerSaveData
        {
            version = 1,
            totalShots = totalShots,
            bins = new OptimizerBinEntry[numBins]
        };

        for (int i = 0; i < numBins; i++)
        {
            data.bins[i] = new OptimizerBinEntry
            {
                center = bins[i].center,
                qualitySum = bins[i].qualitySum,
                weightSum = bins[i].weightSum,
                shotCount = bins[i].rawShotCount,
                ewma = bins[i].ewma
            };
        }

        try
        {
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(SavePath, json);
            saved = true;
            Debug.Log($"[SensitivityOptimizer] Model saved ({totalShots} shots).");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SensitivityOptimizer] Save failed: {e.Message}");
        }
    }

    // load a previously saved model and apply session decay to old data
    public void LoadModel()
    {
        if (!File.Exists(SavePath)) return;

        try
        {
            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<OptimizerSaveData>(json);

            if (data == null || data.bins == null || data.bins.Length != numBins)
            {
                Debug.LogWarning("[SensitivityOptimizer] Incompatible save data - starting fresh.");
                return;
            }

            // apply decay so older sessions don't dominate newer ones
            for (int i = 0; i < numBins; i++)
            {
                bins[i].qualitySum = data.bins[i].qualitySum * sessionDecay;
                bins[i].weightSum = data.bins[i].weightSum * sessionDecay;
                bins[i].rawShotCount = data.bins[i].shotCount;
                bins[i].ewma = data.bins[i].ewma;
            }

            totalShots = data.totalShots;
            dirty = true;
            saved = false;

            Debug.Log($"[SensitivityOptimizer] Model loaded ({totalShots} historical shots, decay {sessionDecay:F2} applied).");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SensitivityOptimizer] Load failed: {e.Message}");
        }
    }

    // wipe everything - useful if user switches mouse/mousepad
    public void ResetModel()
    {
        InitializeBins();
        totalShots = 0;
        saved = false;
        SaveModel();
        saved = false;
        Debug.Log("[SensitivityOptimizer] Model reset.");
    }

    // maps a sensitivity value to the correct bin index
    private int SensitivityToBinIndex(float sensitivity)
    {
        float t = Mathf.InverseLerp(minSens, maxSens, sensitivity);
        return Mathf.Clamp(Mathf.RoundToInt(t * (numBins - 1)), 0, numBins - 1);
    }
}
