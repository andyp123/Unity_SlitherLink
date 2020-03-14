using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RaycastToDynamicMaterial : MonoBehaviour
{
	private Renderer _renderer;
	private MaterialPropertyBlock _mpb;
	private int _xy1xy2PropID;
	private int _colliderID;

    void Awake()
    {
        _xy1xy2PropID = Shader.PropertyToID("_xy1xy2");
        _mpb = new MaterialPropertyBlock();
        _renderer = GetComponent<Renderer>();

        MeshCollider collider = GetComponent<MeshCollider>();
        if (collider != null)
        {
        	_colliderID = collider.GetInstanceID();
        }
    }

    void Update()
    {
        if (_renderer != null)
        {
		    // Raycast with the scene to get UV coords of hits
        	Vector4 xy1xy2 = new Vector4(0f, 0f, 1f, 1f);

        	RaycastHit hit;
        	if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit))
        	{
        		MeshCollider hitCollider = hit.collider as MeshCollider;
        		if (hitCollider != null && hitCollider.GetInstanceID() == _colliderID)
        		{
		    		xy1xy2.x = hit.textureCoord.x;
		    		xy1xy2.y = hit.textureCoord.y;

                    _mpb.SetVector(_xy1xy2PropID, xy1xy2);
                    _renderer.SetPropertyBlock(_mpb);
		    	}
		    }
        }
    }
}
