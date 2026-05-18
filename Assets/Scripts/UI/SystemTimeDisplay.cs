using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Simple utility to update a Text component with the system time.
/// </summary>
public class SystemTimeDisplay : MonoBehaviour
{
    private Text timeText;

    private void Awake()
    {
        timeText = GetComponent<Text>();
    }

    private void Update()
    {
        if (timeText != null)
            timeText.text = DateTime.Now.ToString("HH:mm");
    }
}