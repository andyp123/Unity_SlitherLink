using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageMenu : MonoBehaviour
{
	public int buttonsPerRow = 5;
	public int numberOfButtons = 15;
	public Vector3 offset = Vector2.zero;

	public float spacing = 1f;

	public GameObject buttonPrefab;
	public Board board;

    void Awake()
    {
    	int x, y;
    	for (int i = 0; i < numberOfButtons; ++i)
    	{
    		x = i % buttonsPerRow;
    		y = (i - x) / buttonsPerRow;
    		GameObject go = GameObject.Instantiate(buttonPrefab, transform.position, Quaternion.identity);
    		RectTransform rt = go.GetComponent<RectTransform>();
    		rt.SetParent(transform);
    		rt.anchoredPosition = new Vector2(offset.x + spacing * x , offset.y + spacing * -y);

    		string stageId = (i + 1).ToString("D2");
    		go.name = stageId;

    		TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>();
    		if (label != null)
    		{
    			label.text = stageId;
    		}

    		Button button = go.GetComponent<Button>();
    		if (button != null)
    		{
    			int levelID = i + 1;
    			button.onClick.AddListener(delegate() { board.SetLevel(levelID); });
    		}
    	}
    }
}
