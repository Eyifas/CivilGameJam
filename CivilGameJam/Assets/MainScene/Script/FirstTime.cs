using UnityEngine;
using TMPro;

public class FirstTime : MonoBehaviour
{
    [SerializeField] private float firstTime = 90f;
    [SerializeField] private TMP_Text timerText;

    private bool timeEnded = false;

    private void Update()
    {
        if (firstTime > 0f)
        {
            firstTime -= Time.unscaledDeltaTime;
            firstTime = Mathf.Max(0f, firstTime);
        }
        else
        {
            if (!timeEnded)
            {
                timeEnded = true;
                Debug.Log("Süre bitti!");
            }
        }

        int totalSeconds = Mathf.CeilToInt(firstTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }
}
