#!/usr/bin/env python3
# SenzAI sensitivity analyzer
# reads the JSON stuff the game saves and prints some stats about it
# also runs a basic ML model on the raw shot data just to double check
# the in-game optimizer's numbers look reasonable

import sys, os, json, glob, math

# ---------- finding the save folder ----------

def find_data_dir():
    # unity dumps saves into a weird OS-specific folder, so we gotta go hunting for it
    if sys.platform == "win32":
        base = os.path.join(os.environ.get("APPDATA", ""), "..", "LocalLow")
    elif sys.platform == "darwin":
        base = os.path.expanduser("~/Library/Application Support")
    else:
        base = os.path.expanduser("~/.config/unity3d")

    base = os.path.normpath(base)
    if not os.path.isdir(base):
        return None

    # unity path looks like .../CompanyName/ProductName/
    # just check every folder for our save files and pick whichever has the most
    best_dir = None
    best_score = 0
    for company in os.listdir(base):
        company_path = os.path.join(base, company)
        if not os.path.isdir(company_path):
            continue
        for product in os.listdir(company_path):
            candidate = os.path.join(company_path, product)
            num_sessions = len(glob.glob(os.path.join(candidate, "session_*.json")))
            has_model = os.path.isfile(os.path.join(candidate, "senzai_optimizer.json"))
            score = num_sessions + (100 if has_model else 0)
            if score > best_score:
                best_score = score
                best_dir = candidate

    return best_dir


# ---------- little print helpers ----------

def bar(value, width=15):
    # makes a simple progress-bar looking string for the terminal
    value = max(0.0, min(value, 1.0))
    filled = round(value * width)
    return "#" * filled + "-" * (width - filled)


# ---------- reading the in-game GP model ----------

def load_optimizer_model(data_dir):
    path = os.path.join(data_dir, "senzai_optimizer.json")
    if not os.path.isfile(path):
        return None
    with open(path) as f:
        return json.load(f)


def print_optimizer_model(model):
    print("-" * 55)
    print("In-game optimizer model")
    print(f"  total shots: {model.get('totalShots', 0)}")

    bins = model.get("bins", [])
    if not bins:
        print("  no bins saved yet")
        return

    # find the best scoring bin so we can mark it
    best_center = 0
    best_avg = -1
    for b in bins:
        weight = b.get("weightSum", 0)
        if weight > 0.5:
            avg = b.get("qualitySum", 0) / weight
            if avg > best_avg:
                best_avg = avg
                best_center = b.get("center", 0)

    print(f"  best sensitivity so far: {best_center:.3f} (quality {best_avg:.2f})")
    print()
    print("  sens     shots   quality")
    for b in bins:
        shots = b.get("shotCount", 0)
        if shots == 0:
            continue
        weight = b.get("weightSum", 0)
        avg = b.get("qualitySum", 0) / weight if weight > 0.5 else 0
        marker = " <-- best" if abs(b.get("center", 0) - best_center) < 0.001 else ""
        print(f"  {b.get('center', 0):.3f}   {shots:4d}   {bar(avg)} {avg:.2f}{marker}")
    print()


# ---------- raw session json stuff ----------

def load_sessions(data_dir):
    sessions = []
    for path in sorted(glob.glob(os.path.join(data_dir, "session_*.json"))):
        try:
            with open(path) as f:
                sessions.append(json.load(f))
        except Exception as e:
            print(f"  couldn't read {os.path.basename(path)}: {e}")
    return sessions


def get_all_shots(sessions):
    # just flattens every shot from every session into one list
    shots = []
    for s in sessions:
        shots.extend(s.get("shots") or [])
    return shots


def print_session_stats(shots):
    total = len(shots)
    hits = sum(1 for s in shots if s.get("hit"))

    print("-" * 55)
    print(f"Raw session data ({total} shots)")
    print(f"  hits: {hits}")
    print(f"  accuracy: {hits / total * 100:.1f}%" if total else "  accuracy: n/a")

    devs = [s["aimDeviation"] for s in shots if (s.get("aimDeviation") or -1) >= 0]
    if devs:
        print(f"  avg deviation: {sum(devs) / len(devs):.1f} deg")

    rts = [s["reactionTime"] for s in shots if (s.get("reactionTime") or -1) >= 0]
    if rts:
        print(f"  avg reaction time: {sum(rts) / len(rts):.2f}s")
    print()


# ---------- optional sklearn sanity check ----------

def run_ml_check(shots, model):
    try:
        import numpy as np
        from sklearn.ensemble import GradientBoostingRegressor
    except ImportError:
        print("(skipping ML check, sklearn not installed)")
        return

    # build training data: [sensitivity, distance, reaction time] -> quality
    X, y = [], []
    for s in shots:
        sens = s.get("sensitivity", 0)
        if not sens or sens < 0.01 or sens > 0.5:
            continue

        dev = s.get("aimDeviation", -1)
        hit = s.get("hit", False)
        quality = 1.0 / (1.0 + max(0, dev) / 3.0) if hit and dev is not None and dev >= 0 else 0.0

        dist = s.get("distance", 0) or 0
        rt = s.get("reactionTime", 0) or 0

        X.append([sens, max(0, dist), max(0, rt)])
        y.append(quality)

    if len(X) < 20:
        print(f"not enough shots for the ML check yet ({len(X)}, need 20)")
        return

    # trees don't need scaling so just fit straight on the raw features
    model_gb = GradientBoostingRegressor(n_estimators=200, learning_rate=0.05, max_depth=3)
    model_gb.fit(X, y)

    # sweep sensitivity values, keep distance/reaction time fixed at their medians
    X = np.array(X)
    med_dist = float(np.median(X[:, 1]))
    med_rt = float(np.median(X[:, 2]))

    test_sens = np.linspace(0.01, 0.5, 200)
    test_rows = [[s, med_dist, med_rt] for s in test_sens]
    preds = model_gb.predict(test_rows)

    best_sens = float(test_sens[int(np.argmax(preds))])

    print("-" * 55)
    print("sklearn check (gradient boosting)")
    print(f"  model says best sens: {best_sens:.3f}")

    if model:
        bins = model.get("bins", [])
        best_center, best_avg = 0, -1
        for b in bins:
            weight = b.get("weightSum", 0)
            if weight > 0.5:
                avg = b.get("qualitySum", 0) / weight
                if avg > best_avg:
                    best_avg = avg
                    best_center = b.get("center", 0)

        diff = abs(best_sens - best_center)
        if diff < 0.03:
            print(f"  matches in-game model ({best_center:.3f}) - good sign")
        else:
            print(f"  in-game model says {best_center:.3f}, off by {diff:.3f}")
    print()


# ---------- main ----------

def main():
    print()
    print("=== SenzAI sensitivity analyzer ===")
    print()

    data_dir = find_data_dir()
    if not data_dir:
        print("couldn't find the unity save folder, has the game been run yet?")
        return

    print(f"reading from: {data_dir}")
    print()

    model = load_optimizer_model(data_dir)
    if model:
        print_optimizer_model(model)
    else:
        print("no optimizer model saved yet, play a session first")
        print()

    sessions = load_sessions(data_dir)
    if not sessions:
        print("no session files found")
        return

    shots = get_all_shots(sessions)
    print_session_stats(shots)

    if len(shots) >= 20:
        run_ml_check(shots, model)

    print("-" * 55)
    if model:
        print("looks good, in-game optimizer has data")
    else:
        print("no in-game model yet, keep playing to build one up")
    print()


if __name__ == "__main__":
    main()
