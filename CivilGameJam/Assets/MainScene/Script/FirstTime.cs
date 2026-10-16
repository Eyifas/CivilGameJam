using UnityEngine;
using TMPro;

public class FirstTime : MonoBehaviour
{
    [SerializeField] private float firstTime = 90f;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private RoomCamera roomCamera;

    private bool timeEnded = false;

    private void Update()
    {
        if (!timeEnded)
        {
            firstTime -= Time.unscaledDeltaTime;
            firstTime = Mathf.Max(0f, firstTime);

            if (firstTime <= 0f)
            {
                timeEnded = true;

                if (roomCamera != null)
                {
                    roomCamera.StartEarthquake();
                }

                Debug.Log("Süre bitti!");
            }
        }

        int totalSeconds = Mathf.CeilToInt(firstTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        if (timerText != null)
        {
            timerText.text =
                minutes.ToString("00") + ":" + seconds.ToString("00");
        }
    }
}