using UnityEngine;
using System.Collections;

/// <summary>
/// This class allows to easily locate buttons and other UI elements in our
/// screen.
/// 
/// Position is given as a fraction of the screen size, relative to the CENTRE
/// of the escreen. Then, xPosition = 0 = yPosition will be the centre.
/// yPosition = -0.5 corresponds to the lower edge of the screen.
/// </summary>
public class ScreenSpaceButtonStarterEvolution : MonoBehaviour {

    // Position will be given as a fraction of the local screen (left-side
    // or right-side screen), with lower-left corners as reference
    public float xPosition;
    public float yPosition;
    // The positions are relative to the screen size. xOffsetPixels allows to
    // offset this result by a given (absolute) number of pixels!
    public float xOffsetPixels;
    public float yOffsetPixels;

    /// <summary>
    /// The position of the button is done in Start, because in Awake the 
    /// canvas is not ready (reports size = 0)
    /// 
    /// 0,0 is at the centre for canvas, but the lower left corner for the viewport.
    /// </summary>
    void Start()
    {
        Vector2 objectPosition = new Vector2();

        objectPosition.x = Screen.width * xPosition;
        objectPosition.y = Screen.height * yPosition;
        
        // Adds the desired offset:
        objectPosition.x += xOffsetPixels;
        objectPosition.y -= yOffsetPixels;

        transform.localPosition = objectPosition;         
    }
}