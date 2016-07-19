using UnityEngine;
using System.Collections;
using SharpNeat.Phenomes;

public class XorController : UnitController
{  
    //IsRunning is needed here because of the magic as fas as I know  
    bool IsRunning;
    //box is the neural network that will actually control the unit
    IBlackBox box;  

    private static int ID_counter = 0;
    private static Camera evolutionCamera;
    private static bool gotCamera = false;

    // Used for the correct lattice display.
    private static float xIni;
    private static float zIni;
    //private static float spacing = 2.5f; // for normal scale
    private static float spacing = 2.0f; // for scale 0,8
    private static int widthFit;

    private float input_1;
    private float input_2;
    private float fitness;
    private float output00;
    private float output01;
    private float output10;
    private float output11;

    private Renderer render00;
    private Renderer render01;
    private Renderer render10;
    private Renderer render11;

    void Start ()
    {
        // FindWithTag is rather slow, so we only look for the camera once.
        if (!gotCamera)
        {
            evolutionCamera = GameObject.Find("_ESPelements").transform.Find("Cameras").
                              transform.Find("EvolutionCamera").GetComponent<Camera>();

            // The position of the elements depends on the camera settings.
            // starting position for XOR normal scale
            //Vector3 position = evolutionCamera.ViewportToWorldPoint(new Vector3(0.15f, 0.65f, 11));
            // starting position for XOR scale 0,8
            Vector3 position = evolutionCamera.ViewportToWorldPoint(new Vector3(0.1f, 0.65f, 11));
            xIni = position.x;
            zIni = position.z;
            // max position for XOR normal scale
            // position = evolutionCamera.ViewportToWorldPoint(new Vector3(0.9f, 0.65f, 11));
            // max position for XOR scale 0,8
            position = evolutionCamera.ViewportToWorldPoint(new Vector3(0.97f, 0.65f, 11));
            float xMax = position.x;

            // However sound my reasoning might have been to choose 0.9f as the
            // xMax reference, the fact is we can easily fit one more.
            widthFit = (int)(Mathf.Abs(xMax - xIni) / spacing) + 1;

            Debug.Log("ini " + xIni + " fin " + xMax + " fit " + widthFit);

            gotCamera = true;
        }

        fitness = 0f;
        SetPosition();
        ++ID_counter;
        // Finds all the parts of our game object. We can change the colour
        // of each to indicate the result (or we could print the number on
        // top).
        render00 = this.transform.Find("Result00").GetComponent<Renderer>();
        render01 = this.transform.Find("Result01").GetComponent<Renderer>();
        render10 = this.transform.Find("Result10").GetComponent<Renderer>();
        render11 = this.transform.Find("Result11").GetComponent<Renderer>();
    }

    void OnDestroy()
    {
        --ID_counter;   
    }	

    //Used (hopefully well) in Optimizer --> DestroyBest
    public override IBlackBox GetBox()
    {
        return box;
    }
	
    public override void Stop()
    {
        this.IsRunning = false;
    }

    public override void Activate(IBlackBox box)
    {
        this.box = box;
        this.IsRunning = true;
    }

    public override float GetFitness()
    {
        fitness = 0f;
        // Avoid testing floats for equality.
		fitness += 1f - output00; // so +1 if output00 = 0
		fitness += 1f - output11;
		fitness += output01; // so +0 if output01 = 0, +1 if output01 = 1, etc.
		fitness += output10;
/*      if (output00 < 0.5f)
        {
            ++fitness;
        }
        if (output01 > 0.5f)
        {
            ++fitness;
        }
        if (output10 > 0.5f)
        {
            ++fitness;
        }
        if (output11 < 0.5f)
        {
            ++fitness;
        }*/

        return fitness;
    }

    //public int GetID() {
    //  return member_ID;
    //}

    public int GetTotal()
    {
        return ID_counter;
    }

    void FixedUpdate () {    
        //if (IsRunning) {
        input_1 = 0f;
        input_2 = 0f;
        output00 = ProcessInputPair(render00);
		// If output00 is below 0.5 we make it 0, so
		// fitness is not concerned about this value.
		if (output00 < 0.5f)
		{
			output00 = 0f;
		}
        Paint(render00, output00);

        input_1 = 0f;
        input_2 = 1f;
        output01 = ProcessInputPair(render01);
		// If output00 is below 0.5 we make it 0, so
		// fitness is not concerned about this value.
		if (output01 > 0.5f)
		{
			output01 = 1f;
        }
        Paint(render01, output01);

        input_1 = 1f;
        input_2 = 0f;
        output10 = ProcessInputPair(render10);
		// If output00 is below 0.5 we make it 0, so
		// fitness is not concerned about this value.
		if (output10 > 0.5f)
		{
			output10 = 1f;
        }
        Paint(render10, output10);

        input_1 = 1f;
        input_2 = 1f;
        output11 = ProcessInputPair(render11);
		// If output00 is below 0.5 we make it 0, so
		// fitness is not concerned about this value.
		if (output11 < 0.5f)
		{
			output11 = 0f;
        }
        Paint(render11, output11);

/*		Debug.Log("out00 " + output00);
		Debug.Log("out01 " + output01);
		Debug.Log("out10 " + output10);
		Debug.Log("out11 " + output11);*/

        // Debug.Log("output 11 " + output11);
        //}    
    
        //UnityEditor.EditorApplication.isPlaying = false;    
    }

    /// <summary>
    /// This method is used if we want more detailed information in the quadrant
    /// colour.
    /// </summary>
    /// <param name="quadrant">Quadrant.</param>
    void Paint(Renderer quadrant, float darkness)
    {
        Color myColor = Color.white;
        myColor.r = myColor.g = myColor.b = 1f - darkness;
        quadrant.material.color = myColor;
    }

    float ProcessInputPair(Renderer quadrant)
    {
        float output = 0f;

        //Input signals are used in the neural controller
        ISignalArray inputArr = box.InputSignalArray;
        inputArr[0] = input_1;
        inputArr[1] = input_2;
        //The neural network is activated
        box.Activate();
        //And produces output signals (also in an array)
        ISignalArray outputArr = box.OutputSignalArray;   

        //Output is between 0 and 1
		output = (float)outputArr[0];

/*		ISignalArray inputArr = box.InputSignalArray;
		inputArr[0] = 0.88f;
		inputArr[1] = 0.99f;
		//inputArr[0] = input_1;
		//inputArr[1] = input_2;
		//The neural network is activated
		box.Activate();
		//And produces output signals (also in an array)
		ISignalArray outputArr = box.OutputSignalArray;   
		//Output is between 0 and 1
		output = (float)outputArr[0];
		Debug.Log(" ");
		Debug.Log("Input array: " + inputArr[0] + " " + inputArr[1]);
		Debug.Log("Output: " + output);
		UnityEditor.EditorApplication.isPlaying = false;*/

        return output;
    }

    void SetPosition()
    {
        Vector3 temp = transform.position;
        temp.x = xIni - (ID_counter % widthFit) * spacing;
        temp.z = zIni + ((ID_counter / widthFit)) * spacing;
        transform.position = temp;
    }

    /// <summary>
    /// Used to identify the 4 children associated to this unit.
    /// </summary>
    GameObject FindChildWithTag(Transform parent_transform, string tag)
    {
        foreach (Transform child_transform in parent_transform)
        {
            if (child_transform.tag == tag)
            {
                return child_transform.gameObject;
            }
        }
        return null;
    }
}