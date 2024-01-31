using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Scoreboard : MonoBehaviour
{
    
    public TextMeshProUGUI lastRunText;
    public TextMeshProUGUI avgRunText;
    public TextMeshProUGUI bestRunText;


    void Start()
    {
        ChallengeMetrics last = new ChallengeMetrics("last");
        ChallengeMetrics avg = new ChallengeMetrics("avg");
        ChallengeMetrics best = new ChallengeMetrics("best");
        lastRunText.text = last.ToString();
        avgRunText.text = avg.ToString() + "\n\n" + "Total Runs: " + PlayerPrefs.GetInt("total_runs", 0);
        bestRunText.text = best.ToString();
    }
}
