using UnityEngine;

namespace RobotArtist
{
public class Joint
{
    // This is the actual game object (needed to move it!)
    public GameObject jointObject;
    // We remember the position (or angle) of each joint. Needed to ensure
    // we don't move beyond boundaries.
    public float currentValue;
    public float speed;
    // Min and max are the boundaries for the joint.
    public float min;
    public float max;
    // This is the amount (position, angle) we want to move the joint next
    // time MoveAll is called.
    public float toMove;

    public Joint(GameObject p1, float p2, float p3, float p4, float p5, float p6)
    {
        jointObject = p1;
        currentValue = p2;
        speed = p3;
        min = p4;
        max = p5; 
        toMove = p6;
    }
    public void AddAndClamp()
    {
        currentValue += toMove;
        Mathf.Clamp(currentValue, min, max); 
    }
    public void RotateX()
    {
        jointObject.transform.localEulerAngles = new Vector3(
            currentValue, jointObject.transform.localEulerAngles.y,
            jointObject.transform.localEulerAngles.z);
    }
    public void RotateZ()
    {
        jointObject.transform.localEulerAngles = new Vector3(
            jointObject.transform.localEulerAngles.x,
            jointObject.transform.localEulerAngles.y, currentValue);
    }
    public void MoveX()
    {
        jointObject.transform.localPosition = new Vector3(
            currentValue, jointObject.transform.localPosition.y,
            jointObject.transform.localPosition.z);
    }
}
}