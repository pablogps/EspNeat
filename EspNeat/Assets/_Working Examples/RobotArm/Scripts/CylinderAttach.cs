using UnityEngine;
using System.Collections;

public class CylinderAttach : MonoBehaviour {

    void OnTriggerEnter(Collider other) {

        if (other.name == "ManipulatorTip")
        {
            other.GetComponent<ManipulatorTip>().PickThisUp(this.GetComponent<Rigidbody>());
            // So that it follows neatly!
            this.GetComponent<Rigidbody>().mass = 0.0f;

            // This does almost works, but the attatchment is clucky!
/*            transform.SetParent(other.transform);
            // Or else it falls
            this.GetComponent<Rigidbody>().useGravity = false;
            // Or else it flies away!
            this.GetComponent<Rigidbody>().drag = 1000.0f;*/
        }
    }

    public void ResetFree() {
        this.GetComponent<Rigidbody>().mass = 0.05f;
    }
}
