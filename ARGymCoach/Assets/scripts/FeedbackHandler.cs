using UnityEngine;
using System.Collections.Generic;
using System.Linq;


public class FeedbackHandler : MonoBehaviour
{
    [SerializeField] private GeminiVoiceSimple GVS;

    public void handleFeedback(List<int> feedbackList) {
        int lower_good = feedbackList.Count(x => x == 0);
        int middle_good = feedbackList.Count(x => x == 1);
        int upper_good = feedbackList.Count(x => x == 2);
        int left_bad = feedbackList.Count(x => x == 3);
        int right_bad = feedbackList.Count(x => x == 4);
        int wide_bad = feedbackList.Count(x => x == 5);
        bool anyBad = false;

        string result = "";
        if (left_bad >= 6)
        {
            result = result + "0";
            anyBad = true;
        }
        if (right_bad >= 4)
        {
            result = result + "1";
            anyBad = true;
        }
        if (wide_bad >= 10)
        {
            result = result + "2";
            anyBad = true;
        }
        if (lower_good < 15 && !anyBad)
        {
            result = result + "3";
        }
        if (upper_good < 15 && !anyBad)
        {
            result = result + "4";
        }

        GVS.Send(result);
        UnityEngine.Debug.Log($"Feedback: {result}");
        UnityEngine.Debug.Log($"Lower good: {lower_good}, Middle good: {middle_good}, Upper good: {upper_good}, Left bad: {left_bad}, Right bad: {right_bad}, Wide bad: {wide_bad}");
    }
}
