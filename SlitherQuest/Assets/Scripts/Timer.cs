using UnityEngine;
using TMPro;

[System.Serializable]
public class Timer
{
	public string prefix = "Time: ";
    public bool paused = false;
	public TextMeshPro textComponent;

	protected float _timeElapsed = 0f;
	public float TimeElapsed
	{
		get { return _timeElapsed; }
		set { _timeElapsed = Mathf.Clamp(value, 0f, 5999f); }
	}

    public void Update ()
    {
        if (!paused)
        {
            _timeElapsed += Time.deltaTime;
            SetText();
        }
    }

    public void Reset (bool startPaused = true)
    {
    	_timeElapsed = 0f;
    	paused = startPaused;
        SetText();
    }

    public void SetVisibility (bool state)
    {
    	if (textComponent != null)
    	{
    		textComponent.gameObject.SetActive(state);
    	}
    }

    void SetText ()
    {
    	if (textComponent != null)
    	{
	        int minutes = (int)Mathf.Floor(_timeElapsed / 60f);
	        int seconds = (int)Mathf.Floor(_timeElapsed) % 60;
	        textComponent.text = string.Format("{0}{1}:{2}", prefix, minutes.ToString("D2"), seconds.ToString("D2"));
    	}
    }
}