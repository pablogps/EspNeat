using UnityEngine;
using System.Collections;

public class DirectionSignalController : MonoBehaviour {
	
    public bool isLeft = true;
    public bool isStraight = true;
    public bool isRight = true;
    private int numberChoices;

    private GameObject directionArrow = null;
    //private Vector3 originalRotation = new Vector3();
    private Quaternion originalRotation = new Quaternion();

    // Traffic light period (note the maximum period will actually be
    // maxPeriod + minPeriod)
    private float maxPeriod = 6f;
    private float minPeriod = 2f;
    private float period;
    private float elapsed_time = 0f;

	private enum arrowDirection
	{
		Left,
        Right,
        Straight,
        Off
	}

    private arrowDirection currentDirection = new arrowDirection();

	void Awake()
	{
        CountChoices();

        // Gets the length for the first clock period
        NewPeriod();

        // Gets a reference to the direction arrow
        directionArrow = transform.FindChild("Signal").gameObject;
        // We have to copy the original rotation like this, to avoid creating
        // a reference! (Which is bad, since our rotation copy would rotate
        // with the texture.)
        //originalRotation = new Vector3(directionArrow.transform.eu);
        originalRotation = Quaternion.Euler(directionArrow.transform.eulerAngles.x,
                                            directionArrow.transform.eulerAngles.y,
                                            directionArrow.transform.eulerAngles.z);
        
        // Starts with a random allowed direction (we initialize it as straight
        // to match the initial texture orientation)
        currentDirection = arrowDirection.Straight;
        StartDirection();
	}

    /// <summary>
    /// Used to change the traffic light after the waiting period
    /// </summary>
    void Update()
    {
        elapsed_time += Time.deltaTime;
        if (elapsed_time > period)
        {
            //we reset our timer
            elapsed_time = 0f;
            NewPeriod();
            //and change the clock
            NewDirection();
        }   
    }

    /// <summary>
    /// This returns a float to be used as input by neural networks.
    /// Note that no signal is not the same as straight (driving strategy may
    /// be quite different for those cases!)
    /// </summary>
    public float GetDirectionAsFloat()
    {
        switch (currentDirection)
        {
        case arrowDirection.Left:
            return 0.333f;
            break;
        case arrowDirection.Straight:
            return 0.667f;
            break;
        case arrowDirection.Right:
            return 1f;  
            break;
        default:
            return 0f;  
            break;
        }        
    }

    /// <summary>
    /// This is useful for special cases like only one option or no options!
    /// </summary>
    private void CountChoices()
    {
        numberChoices = 0;
        if (isLeft)
        {
            ++numberChoices;
        }
        if (isStraight)
        {
            ++numberChoices;
        }
        if (isRight)
        {
            ++numberChoices;
        }

        // If there is only one option, let us avoid trying to change lights...
        if (numberChoices == 1)
        {
            // As good as infinity!
            minPeriod = 10000;
        }
    }

    /// <summary>
    /// Gets the first direction
    /// </summary>
    private void StartDirection()
    {
        if (numberChoices == 0)
        {
            currentDirection = arrowDirection.Off;
            directionArrow.SetActive(false);
        }
        else
        {
            NewDirection();
        }
    }

    /// <summary>
    /// Sets the next period for the traffic light
    /// </summary>
    private void NewPeriod()
    {
        period = Random.value * maxPeriod + minPeriod;
    }

    /// <summary>
    /// Sets a valid direction
    /// </summary>
    private void NewDirection()
    {
        // Easy way to get a random, valid choice
        while (true)
        {
            float myRandom = Random.value;
            if (myRandom < 0.333f)
            {
                // Is left allowed?
                if (isLeft)
                {
                    currentDirection = arrowDirection.Left;
                    break;
                }
            }
            else if (myRandom < 0.667f)
            {
                // Is straight allowed?
                if (isStraight)
                {
                    currentDirection = arrowDirection.Straight;
                    break;
                }
            }
            else
            {
                // Is right allowed?
                if (isRight)
                {
                    currentDirection = arrowDirection.Right;
                    break;
                } 
            }
        }

        // Set the texture to the correct orientation
        DirectionChange();
    }

    /// <summary>
    /// The direction has changed and the texture must be rotated
    /// </summary>
	private void DirectionChange()
    {
        // Remember we must avoid creating a reference between the current
        // rotation and the saved value.
        directionArrow.transform.rotation = Quaternion.Euler(
                originalRotation.eulerAngles.x, originalRotation.eulerAngles.y,
                originalRotation.eulerAngles.z);

        // For rotating-thing's reasons the old y axis seems to be now z
        if (currentDirection == arrowDirection.Left)
        {
            directionArrow.transform.Rotate(new Vector3(0f, 0f, 90f));
        }
        else if (currentDirection == arrowDirection.Right)
        {
            directionArrow.transform.Rotate(new Vector3(0f, 0f, -90f));
        }
	}
}
