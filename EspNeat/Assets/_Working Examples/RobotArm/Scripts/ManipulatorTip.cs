using UnityEngine;
using System.Collections;

public class ManipulatorTip : MonoBehaviour {
    
    private bool isFree = true;
    private GameObject cargo = null;

    public void PickThisUp(Rigidbody otherbody) {
        if (isFree)
        {
            // This is so that we avoid creating countless joints!
            // Also it makes sense that the manipulator can only pick up one
            // thing at a time.
            isFree = false;
            //gameObject.AddComponent<FixedJoint>();
            gameObject.GetComponent<FixedJoint>().connectedBody = otherbody;
            cargo = otherbody.gameObject;
        }
    }

    public void Release() {
        if (!isFree)
        {
            isFree = true;
            if (cargo.GetComponent<CylinderAttach>() != null)
            {
                cargo.GetComponent<CylinderAttach>().ResetFree();
            }
            gameObject.GetComponent<FixedJoint>().connectedBody = null;
            cargo = null;
        }
    }
}
