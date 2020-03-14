using UnityEngine;
using TMPro;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

public class Board : MonoBehaviour
{
	[Header("Settings")]
	[Range(1,100)]
	public int sizeX = 4;
	[Range(1,100)]
	public int sizeY = 3;
	private Cell[] _cells;

	[Header("Models and Materials")]
	public Material gridMaterial;
	public Material cellEdgeMaterial;
	public Material errorMaterial;
	public Mesh cellMesh;
	public Mesh gridEdgeMesh;
	public Mesh gridCornerMesh;
	public Mesh[] cellNumberMeshes;
	public Mesh[] cellEdgeMeshes;

	[Header("GUI Elements")]
	// public TextMeshPro timerText;
	public TextMeshPro puzzleEditText;
	public Timer timer;

	// Cell and edge detection parameters
	protected float edgeTolerance = 0.5f;
	protected Vector3 _cursorXY = Vector3.zero;
	protected CursorValidity _cursorValidity = CursorValidity.Outside;
	protected int _hoverCellX = 0;
	protected int _hoverCellY = 0;
	protected EdgeID _nearestEdgeID;

	// Cell Rendering
	protected MeshFilter[] _cellNumberMeshFilters;
	protected MeshRenderer[] _cellNumberRenderers;
	protected MeshFilter[] _cellEdgeMeshFilters;
	protected int _levelID = 0;

	protected bool inEditMode = false;
	protected bool showSolution = false;

	protected Transform gridRoot;

	public enum EdgeID : byte
	{
		None = 0,
		Top = 1,
		Bottom = 2,
		Left = 4,
		Right = 8,
	}

	public enum CursorValidity
	{
		Outside,
		InsideCell,
		CloseToCell,
	}

	public class Cell
	{
		public bool showRequiredEdgeCount = true;

		private byte _activeEdgeIDs = 0;
		private byte _requiredEdgeCount = 0;
		private byte _currentEdgeCount = 0;
		private byte _solutionEdgeIDs = 0; // stores the original solution to the puzzle

		public Cell () {}
		public Cell (CellSerializeData data, bool solve)
		{
			_solutionEdgeIDs = data.solutionEdgeIDs;
			_requiredEdgeCount = GetEdgeCountFromIDs(_solutionEdgeIDs);
			showRequiredEdgeCount = data.showRequiredEdgeCount;
			if (solve)
			{
				_activeEdgeIDs = _solutionEdgeIDs;
			}
		}

		public byte ActiveEdgeIDs
		{
			get { return _activeEdgeIDs; }
		}

		public byte SolutionEdgeIDs
		{
			get { return _solutionEdgeIDs; }
		}

		public byte RequiredEdgeCount
		{
			get { return _requiredEdgeCount; }
			set { _requiredEdgeCount = (byte)Mathf.Clamp((int)value, 0, 3); }
		}

		public byte CurrentEdgeCount
		{
			get { return _currentEdgeCount; }
		}

		private void UpdateCurrentEdgeCount ()
		{
			_currentEdgeCount = GetEdgeCountFromIDs(_activeEdgeIDs);
		}

		public byte GetEdgeCountFromIDs (byte edgeIDs)
		{
			byte edgeCount = 0;
			if ((edgeIDs & (byte)EdgeID.Top)    > 0) { edgeCount += 1; }
			if ((edgeIDs & (byte)EdgeID.Bottom) > 0) { edgeCount += 1; }
			if ((edgeIDs & (byte)EdgeID.Left)   > 0) { edgeCount += 1; }
			if ((edgeIDs & (byte)EdgeID.Right)  > 0) { edgeCount += 1; }
			return edgeCount;		
		}

		public bool EdgeCheck ()
		{
			return (_currentEdgeCount == _requiredEdgeCount);
		}

		public void SetEdge (EdgeID edgeID, bool state)
		{
			if (state)
			{
				_activeEdgeIDs |= (byte)edgeID;
			}
			else
			{
				_activeEdgeIDs &= (byte)~(byte)edgeID;
			}

			UpdateCurrentEdgeCount();
		}

		public void ToggleEdge (EdgeID edgeID)
		{
			bool state = ((_activeEdgeIDs & (byte)edgeID) > 0) ? false : true;
			SetEdge(edgeID, state);
		}

		[System.Serializable]
		public struct CellSerializeData
		{
			public byte solutionEdgeIDs;
			public bool showRequiredEdgeCount;
		}

		public CellSerializeData GetSerializeData ()
		{
			CellSerializeData data = new CellSerializeData();
			data.solutionEdgeIDs = _activeEdgeIDs;
			data.showRequiredEdgeCount = showRequiredEdgeCount;

			return data;
		}
	}

	void Awake ()
	{
		timer.Reset();
		InitializeBoard();

		puzzleEditText.gameObject.SetActive(inEditMode);
		timer.SetVisibility(!inEditMode);
	}

	void Update ()
	{
		timer.Update();

		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.E))
		{
			inEditMode = !inEditMode;
			puzzleEditText.gameObject.SetActive(inEditMode);
			timer.SetVisibility(!inEditMode);
			return;
		}

		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.C))
		{
            InitializeBoard();
            return;
        }

		if (Input.GetKeyDown(KeyCode.F5))
		{
			SaveData();
			return;
		}

		if (Input.GetKeyDown(KeyCode.F9))
		{
			LoadData();
			timer.Reset(false);
			return;
		}

		if (Input.GetKey(KeyCode.S))
		{
			if (showSolution != true)
			{
				ShowSolution(true);
			}
		}
		else
		{
			if (showSolution == true)
			{
				ShowSolution(false);
			}
		}

		// Update cursor position and validity
		_cursorXY = Camera.main.ScreenToWorldPoint(Input.mousePosition);
		_cursorXY.z = 0f;
		if (_cursorXY.x > 0f && _cursorXY.x < (float)sizeX && _cursorXY.y > 0f && _cursorXY.y < (float)sizeY)
		{
			_cursorValidity = CursorValidity.InsideCell;
		}
		else if (_cursorXY.x > 0f - edgeTolerance && _cursorXY.x < (float)sizeX + edgeTolerance
		      && _cursorXY.y > 0f - edgeTolerance && _cursorXY.y < (float)sizeY + edgeTolerance)
		{
			_cursorValidity = CursorValidity.CloseToCell;
		}
		else
		{
			_cursorValidity = CursorValidity.Outside;
		}
		
		// Find cell nearest to cursor (assumes normalized cell size)
		Vector3 cellXY = new Vector3(Mathf.Clamp(_cursorXY.x, 0f, (float)(sizeX-1)), Mathf.Clamp(_cursorXY.y, 0f, (float)(sizeY-1)), 0f);
		_hoverCellX = (int)Mathf.Floor(cellXY.x);
		_hoverCellY = (int)Mathf.Floor(cellXY.y);

		_nearestEdgeID = EdgeID.None;
		if (_cursorValidity != CursorValidity.Outside)
		{
			Vector3 cellCenter = new Vector3(_hoverCellX + 0.5f, _hoverCellY + 0.5f, 0f);
			Vector3 ofs = _cursorXY - cellCenter;

			if (ofs.y >= Mathf.Abs(ofs.x))
			{
				_nearestEdgeID = EdgeID.Top;
			}
			else if (ofs.y < -Mathf.Abs(ofs.x))
			{
				_nearestEdgeID = EdgeID.Bottom;
			}
			else if (ofs.x < 0f)
			{
				_nearestEdgeID = EdgeID.Left;
			}
			else
			{
				_nearestEdgeID = EdgeID.Right;
			}
		}

		if (Input.GetMouseButtonDown(0))
		{
			if (_nearestEdgeID != EdgeID.None)
			{
				int i = xyToIndex(_hoverCellX, _hoverCellY);
				// Debug.Log(string.Format("Cell {0}: [{1},{2}] {3}", i, _hoverCellX, _hoverCellY, _nearestEdgeID));

				int neighborIndex = -1;
				EdgeID neighborEdgeID = EdgeID.None;
				switch (_nearestEdgeID)
				{
				case EdgeID.Left:
					if (_hoverCellX > 0) { neighborIndex = i - 1; neighborEdgeID = EdgeID.Right; }
					break;
				case EdgeID.Right:
					if (_hoverCellX < sizeX - 1) { neighborIndex = i + 1; neighborEdgeID = EdgeID.Left; }
					break;
				case EdgeID.Bottom:
					if (_hoverCellY > 0) { neighborIndex = i - sizeX; neighborEdgeID = EdgeID.Top; }
					break;
				case EdgeID.Top:
					if (_hoverCellY < sizeY - 1) { neighborIndex = i + sizeX; neighborEdgeID = EdgeID.Bottom; }
					break;
				default:
					break;
				}

				if (Input.GetKey(KeyCode.LeftControl))
				{
					if (inEditMode)
					{
						// hide/show numbers
						bool state = !_cells[i].showRequiredEdgeCount;
						_cells[i].showRequiredEdgeCount = state;
						_cellNumberMeshFilters[i].gameObject.SetActive(state);
					}
				}
				else
				{
					// update edge display
					_cells[i].ToggleEdge(_nearestEdgeID);
					_cellEdgeMeshFilters[i].sharedMesh = cellEdgeMeshes[(int)_cells[i].ActiveEdgeIDs];
					if (neighborIndex != -1)
					{
						_cells[neighborIndex].ToggleEdge(neighborEdgeID);
						_cellEdgeMeshFilters[neighborIndex].sharedMesh = cellEdgeMeshes[(int)_cells[neighborIndex].ActiveEdgeIDs];
					}

					if (inEditMode)
					{					
						// only update edge count numbers in edit mode
						_cellNumberMeshFilters[i].sharedMesh = cellNumberMeshes[_cells[i].CurrentEdgeCount];
						if (neighborIndex != -1)
						{
							_cellNumberMeshFilters[neighborIndex].sharedMesh = cellNumberMeshes[_cells[neighborIndex].CurrentEdgeCount];
						}
					}
					else
					{
						if (IsPuzzleClear())
						{
							Debug.Log("Puzzle is clear! Well done, you're a genius!");
							timer.paused = true;
						}
					}
				}
			}
		}
	}

	void InitializeBoard (BoardSerializeData data = null)
	{
		bool rebuildGrid = true;

		if (data != null)
		{
			Debug.Log("Initializing from data");
			if (data.sizeX == sizeX && data.sizeY == sizeY)
			{
				rebuildGrid = false;
			}
			sizeX = data.sizeX;
			sizeY = data.sizeY;
		}

		sizeX = Mathf.Clamp(sizeX, 1, 100);
		sizeY = Mathf.Clamp(sizeY, 1, 100);

		int numCells = sizeX * sizeY;
		_cells = new Cell[numCells];

		if (data != null)
		{
			for (int i = 0; i < numCells; ++i)
			{
				_cells[i] = new Cell(data.cells[i], inEditMode); // load the solution if in edit mode
			}
		}
		else
		{
			for (int i = 0; i < numCells; ++i)
			{
				_cells[i] = new Cell();
			}
		}

		if (rebuildGrid)
		{
			BuildGrid();
		}
		else
		{
			RefreshGrid();
		}

		PositionCamera();
	}

	// Typically used when loading with the same dimensions
	void RefreshGrid ()
	{
		int numCells = sizeX * sizeY;
		for (int i = 0; i < numCells; ++i)
		{
			Cell cell = _cells[i];
			_cellEdgeMeshFilters[i].sharedMesh = cellEdgeMeshes[(int)cell.ActiveEdgeIDs];
			_cellNumberMeshFilters[i].sharedMesh = cellNumberMeshes[cell.RequiredEdgeCount];
			_cellNumberMeshFilters[i].gameObject.SetActive(cell.showRequiredEdgeCount);
			_cellNumberRenderers[i].sharedMaterial = gridMaterial;
		}
	}

	// Used on initialization or loading with new dimensions
	void BuildGrid ()
	{
		int i, x, y;
		MeshFilter mf;
		MeshRenderer mr;
		int numCells = sizeX * sizeY;

		_cellNumberMeshFilters = new MeshFilter[numCells];
		_cellNumberRenderers = new MeshRenderer[numCells];
		_cellEdgeMeshFilters = new MeshFilter[numCells];

		if (gridRoot != null)
		{
			Transform[] hierarchy = gridRoot.GetComponentsInChildren<Transform>(true);

			foreach (Transform t in hierarchy)
			{
				Destroy(t.gameObject);
			}
		}

		GameObject go = new GameObject("Board Renderers");
		gridRoot = go.transform;
		gridRoot.parent = transform;

		go = new GameObject("Grid");
		go.transform.parent = gridRoot;
		Transform root = go.transform;

		// Corners
		Vector3 pos = new Vector3(0f, 0f, 0.1f);
		string name = "grid_corner";
		go = new GameObject(name);
		mf = go.AddComponent<MeshFilter>();
		mf.sharedMesh = gridCornerMesh;
		mr = go.AddComponent<MeshRenderer>();
		mr.sharedMaterial = gridMaterial;
		go.transform.position = pos;
		go.transform.parent = root;
		go = GameObject.Instantiate(go);
		go.name = name;
		pos.x = sizeX;
		go.transform.position = pos;
		go.transform.parent = root;
		go = GameObject.Instantiate(go);
		go.name = name;
		pos.y = sizeY;
		go.transform.position = pos;
		go.transform.parent = root;
		go = GameObject.Instantiate(go);
		go.name = name;
		pos.x = 0f;
		go.transform.position = pos;
		go.transform.parent = root;

		// Edges
		pos = new Vector3(0f, 0f, 0.1f);
		name = "grid_edge";
		go = GameObject.Instantiate(go);
		go.name = name;
		mf = go.GetComponent<MeshFilter>();
		mf.sharedMesh = gridEdgeMesh;
		go.transform.position = Vector3.zero;
		go.transform.localScale = new Vector3(sizeX, 1f, 1f);
		go.transform.position = pos;
		go.transform.parent = root;

		for (x = 1; x < sizeY + 1; ++x)
		{
			go = GameObject.Instantiate(go);
			go.name = name;
			pos.y += 1f;
			go.transform.position = pos;
			go.transform.parent = root;
		}

		pos = new Vector3(0f, 0f, 0.1f);
		go = GameObject.Instantiate(go);
		go.name = name;
		go.transform.position = Vector3.zero;
		go.transform.eulerAngles = new Vector3(0f, 0f, 90f);
		go.transform.localScale = new Vector3(sizeY, 1f, 1f);
		go.transform.position = pos;
		go.transform.parent = root;

		for (y = 1; y < sizeX + 1; ++y)
		{
			go = GameObject.Instantiate(go);
			go.name = name;
			pos.x += 1f;
			go.transform.position = pos;
			go.transform.parent = root;
		}
	
		// Cell Numbers
		go = new GameObject("Numbers");
		go.transform.parent = gridRoot;
		root = go.transform;

		for (i = 0; i < numCells; ++i)
		{
			indexToXY(i, out x, out y);
			name = string.Format("number_{0}_{1}", x, y);
			go = new GameObject(name);
			go.transform.parent = root;
			go.transform.position = new Vector3((float)x + 0.5f, (float)y + 0.5f, 0f);
			mf = go.AddComponent<MeshFilter>();
			mr = go.AddComponent<MeshRenderer>();
			mf.sharedMesh = cellNumberMeshes[_cells[i].RequiredEdgeCount];
			mr.sharedMaterial = gridMaterial;
			_cellNumberMeshFilters[i] = mf;
			_cellNumberRenderers[i] = mr;
			_cellNumberMeshFilters[i].gameObject.SetActive(_cells[i].showRequiredEdgeCount);
		}

		// Cell Edges
		go = new GameObject("Cell Edges");
		go.transform.parent = gridRoot;
		root = go.transform;

		for (i = 0; i < numCells; ++i)
		{
			indexToXY(i, out x, out y);
			name = string.Format("edge_{0}_{1}", x, y);
			go = new GameObject(name);
			go.transform.parent = root;
			go.transform.position = new Vector3((float)x + 0.5f, (float)y + 0.5f, 0f);
			mf = go.AddComponent<MeshFilter>();
			mr = go.AddComponent<MeshRenderer>();
			mf.sharedMesh = cellEdgeMeshes[(int)_cells[i].ActiveEdgeIDs];
			mr.sharedMaterial = cellEdgeMaterial;
			_cellEdgeMeshFilters[i] = mf;
		}

		// Probably not needed in this game, but this would be the best time...
		System.GC.Collect();
	}

	void PositionCamera ()
	{
		Camera cam = Camera.main;
		float ofsY = sizeY / 2f;
		float ofsX = cam.aspect * (ofsY + 0.5f) - 0.5f;
		cam.transform.position = new Vector3(ofsX, ofsY, -6f);
		cam.orthographicSize = ofsY + 0.5f;
	}

	void indexToXY (int i, out int x, out int y)
	{
		x = i % sizeX;
		y = (int)((i - x) / sizeX);
	}

	int xyToIndex (int x, int y)
	{
		return sizeX * y + x;
	}

	void ShowSolution (bool state)
	{
		int numCells = sizeX * sizeY;
		if (state)
		{
			for (int i = 0; i < numCells; ++i)
			{
				_cellNumberRenderers[i].sharedMaterial = (_cells[i].EdgeCheck()) ? gridMaterial : errorMaterial;
			}
		}
		else
		{
			for (int i = 0; i < numCells; ++i)
			{
				_cellNumberRenderers[i].sharedMaterial = gridMaterial;
			}
		}

		showSolution = state;
	}

	bool IsPuzzleClear ()
	{
		int numCells = sizeX * sizeY;
		for (int i = 0; i < numCells; ++i)
		{
			if (!_cells[i].EdgeCheck())
			{
				return false;
			}
		}

		return true;
	}

	// Serialization ----
	[System.Serializable]
	public class BoardSerializeData
	{
		public int sizeX;
		public int sizeY;
		public Cell.CellSerializeData[] cells;
	}

	public BoardSerializeData GetSerializeData ()
	{
		BoardSerializeData data = new BoardSerializeData();
		int numCells = sizeX * sizeY;
		data.sizeX = sizeX;
		data.sizeY = sizeY;
		data.cells = new Cell.CellSerializeData[numCells];
		for (int i = 0; i < numCells; ++i)
		{
			data.cells[i] = _cells[i].GetSerializeData();			
		}

		return data;
	}

	// From https://www.sitepoint.com/saving-and-loading-player-game-data-in-unity/
	// just to get things going, but ideally want to save to different files
    string GetFilePath (string filename = "")
    {
    	if (filename == "")
    	{
    		filename = string.Format("level{0}.dat", _levelID.ToString("D2"));    		
    	}
        string basePath = (Application.isEditor) ?
            Application.streamingAssetsPath : Application.persistentDataPath;
        return basePath + "/Puzzles/" + filename;     
    }

    public void SaveData()
    {
    	if (_levelID < 1)
    	{
    		Debug.LogError("Cannot save without selecting a level slot.");
    		return;
    	}


    	string path = GetFilePath();

        try
        {
            System.IO.FileInfo finfo = new System.IO.FileInfo(path);
            finfo.Directory.Create();

	        BinaryFormatter formatter = new BinaryFormatter();
	        FileStream file = File.Create(path);

	        BoardSerializeData data = GetSerializeData();
	        formatter.Serialize(file, data);

        	file.Close();
        }
        catch (System.Exception e)
        {
            Debug.LogError(string.Format("Could not save to file '{0}' - {1}", path, e));
            return;
        }

	    #if UNITY_EDITOR // Reimport the file to make sure it appears in the editor folder view
	        UnityEditor.AssetDatabase.ImportAsset("Assets/StreamingAssets" + path.Substring(Application.streamingAssetsPath.Length));
	    #endif
    }

    public void LoadData()
    {
    	string path = GetFilePath();

    	try
    	{
	        BinaryFormatter formatter = new BinaryFormatter();
	        FileStream file = File.Open(path, FileMode.Open);

	        BoardSerializeData data = (BoardSerializeData)formatter.Deserialize(file);
	        InitializeBoard(data);

        	file.Close();
        }
        catch (System.Exception)
        {
            Debug.LogError(string.Format("Could not load file '{0}'.", path));
            return;
        }
    }

    public void SetLevel (int levelID)
    {
    	_levelID = levelID;
    	LoadData();
    }

	void OnDrawGizmos ()
	{
		// Draw Board
		Gizmos.color = new Color(0.0f, 0.5f, 0.0f);

		for (int x = 0; x < sizeX; ++x)
		{
			Gizmos.DrawLine(new Vector3((float)x, 0f, 0f), new Vector3((float)x, (float)sizeY, 0f));
			for (int y = 0; y < sizeY; ++y)
			{
				Gizmos.DrawLine(new Vector3(0f, (float)y, 0f), new Vector3((float)sizeX, (float)y, 0f));
			}
		}

		Gizmos.DrawLine(new Vector3((float)sizeX, 0f, 0f), new Vector3((float)sizeX, (float)sizeY, 0f));
		Gizmos.DrawLine(new Vector3(0f, (float)sizeY, 0f), new Vector3((float)sizeX, (float)sizeY, 0f));

		// Draw Hovered Cell and Edge
		if (_cursorValidity != CursorValidity.Outside)
		{
			// Cell
			Gizmos.color = Color.green;
			Vector3 pos = new Vector3((float)_hoverCellX, (float)_hoverCellY, 0f);
			Gizmos.DrawWireCube(pos + new Vector3(0.5f, 0.5f, 0f), Vector3.one * 0.25f);

			// Edge
			Gizmos.color = Color.green;
			switch (_nearestEdgeID)
			{
			case EdgeID.Bottom:
				Gizmos.DrawLine(pos, pos + new Vector3(1f, 0f, 0f));
				break;
			case EdgeID.Top:
				Gizmos.DrawLine(pos + new Vector3(0f, 1f, 0f), pos + new Vector3(1f, 1f, 0f));
				break;
			case EdgeID.Left:
				Gizmos.DrawLine(pos, pos + new Vector3(0f, 1f, 0f));
				break;
			case EdgeID.Right:
				Gizmos.DrawLine(pos + new Vector3(1f, 0f, 0f), pos + new Vector3(1f, 1f, 0f));
				break;
			default:
				break;
			}
		}

		// Draw cursor
		Gizmos.color = Color.red;
		Gizmos.DrawCube(_cursorXY, Vector3.one * 0.125f);
	}
}