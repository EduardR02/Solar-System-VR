using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct ChallengeMetrics {
    public float time;
    public Vector3 manipulationError;
    public int coins_collected;
    public int score;
    public int challenges;
    
    public ChallengeMetrics(float time, Vector3 manipulationError, int coins_collected, int score, int challenges = 1) {
        this.time = time;
        this.manipulationError = manipulationError;
        this.coins_collected = coins_collected;
        // a bit bad naming and mixing, because this class is used to record a single challenge, and also then to add up all challenges into a run
        // and then it is ALSO used to add up all runs into an average, so it's a bit confusing
        this.challenges = challenges;
        this.score = score;
    }

    public ChallengeMetrics(string prefix) {
        prefix += " ";
        time = PlayerPrefs.GetFloat(prefix + "time", 0);
        manipulationError = new Vector3(
            PlayerPrefs.GetFloat(prefix + "manipulationErrorX", 0),
            PlayerPrefs.GetFloat(prefix + "manipulationErrorY", 0),
            PlayerPrefs.GetFloat(prefix + "manipulationErrorZ", 0)
        );
        coins_collected = PlayerPrefs.GetInt(prefix + "coins_collected", 0);
        score = PlayerPrefs.GetInt(prefix + "score", 0);
        challenges = PlayerPrefs.GetInt(prefix + "challenges", 0);
    }

    public ChallengeMetrics(List<ChallengeMetrics> challenges) {
        this.time = 0;
        this.manipulationError = Vector3.zero;
        this.coins_collected = 0;
        this.score = 0;
        this.challenges = 0;
        foreach (ChallengeMetrics challenge in challenges) {
            this.time += challenge.time;
            this.manipulationError += challenge.manipulationError;
            this.coins_collected += challenge.coins_collected;
            this.score += challenge.score;
            this.challenges += challenge.challenges;
        }
        if (challenges.Count != 0) {
            this.manipulationError /= challenges.Count;
        }
    }

    public void SaveRun(string prefix = "last") {
        prefix += " ";
        PlayerPrefs.SetFloat(prefix + "time", time);
        PlayerPrefs.SetFloat(prefix + "manipulationErrorX", manipulationError.x);
        PlayerPrefs.SetFloat(prefix + "manipulationErrorY", manipulationError.y);
        PlayerPrefs.SetFloat(prefix + "manipulationErrorZ", manipulationError.z);
        PlayerPrefs.SetInt(prefix + "coins_collected", coins_collected);
        PlayerPrefs.SetInt(prefix + "score", score);
        PlayerPrefs.SetInt(prefix + "challenges", challenges);
        if (prefix == "last ") {
            PlayerPrefs.SetInt("total_runs", PlayerPrefs.GetInt("total_runs", 0) + 1);
            PlayerPrefs.Save();
            UpdateAvg();
            if (score > PlayerPrefs.GetInt("best score", 0)) {
                SaveRun("best");
            }
        }
        else {
            PlayerPrefs.Save();
        }
    }

    void UpdateAvg() {
        // takes the old saved average, then calculates a moving average with the new run, then saves it
        ChallengeMetrics avg = new ChallengeMetrics("avg");
        int total_runs = PlayerPrefs.GetInt("total_runs", 1);
        avg.time = (avg.time * (total_runs - 1) + this.time) / total_runs;
        avg.manipulationError = (avg.manipulationError * (total_runs - 1) + this.manipulationError) / total_runs;
        avg.coins_collected = (avg.coins_collected * (total_runs - 1) + this.coins_collected) / total_runs;
        avg.score = (avg.score * (total_runs - 1) + this.score) / total_runs;
        // i just realized this doesnt even work properly because challenge is an int, but whatever
        avg.challenges = (avg.challenges * (total_runs - 1) + this.challenges) / total_runs;
        avg.SaveRun("avg");
    }

    public override string ToString() {
        if (challenges == 0) {
            return "Challenges: -\n" +
            "Time per Challenge: -\n" +
            "Avg Error: -\n" +
            "Coins Collected: -\n" +
            "Total Time: -\n" +
            "Score: -";
        }
        return "Challenges: " + challenges + "\n" +
            "Time per Challenge: " + SecondsToTime(time / challenges) + "\n" +
            "Avg Error: " + manipulationError + "\n" +
            "Coins Collected: " + coins_collected + "\n" +
            "Total Time: " + SecondsToTime(time) + "\n" +
            "Score: " + score;
    }

    string SecondsToTime(float seconds) {
        return ((int) (seconds / 60)) + "m " + ((int) seconds % 60) + "s";
    }
}