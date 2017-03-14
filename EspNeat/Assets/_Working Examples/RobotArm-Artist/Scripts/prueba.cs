using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class prueba : MonoBehaviour {

	private Texture2D texture;
	private Renderer rend;

	void Start() {
		rend = this.GetComponent<Renderer>();
		texture = rend.material.mainTexture as Texture2D;
	}

	void Update()
	{
		// Paints ALL blue
		//rend.material.color =  new Color(0f,0.129f,1f,1f);
    	
		texture = new Texture2D(8, 8);
		for (int  x = 0; x < texture.width; ++x)
		{
			for (int  y = 0; y < texture.height; ++y)
			{
				texture.SetPixel(x, y, Color.red);
			}			
		}
		texture.Apply();
		rend.material.mainTexture = texture;
	}
}
