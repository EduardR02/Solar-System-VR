using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Scoreboard : MonoBehaviour
{
    
    public Text lastRunText;
    public Text avgRunText;
    public Text bestRunText;


    void Start()
    {
        ChallengeMetrics last = new ChallengeMetrics("last");
        ChallengeMetrics avg = new ChallengeMetrics("avg");
        ChallengeMetrics best = new ChallengeMetrics("best");
        lastRunText.text = last.ToString();
        avgRunText.text = avg.ToString() + "\n\n" + PlayerPrefs.GetInt("total_runs", 0);
        bestRunText.text = best.ToString();
    }
}
