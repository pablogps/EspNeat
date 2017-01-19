using UnityEngine;
using System.Collections;

public class Manipulator : MonoBehaviour {

    private WelderController parentScript;

    void Start() {
		// transform.root is problematic if the prefab is instantiated as a
        // child of an object (say within a folder for tidiness)
        //parentScript = transform.root.GetComponent<WelderMovement>();

        // This alternative (less elegant) relies on the fact that the prefab
        // root is alwyas at the same distance, but we will need to fix this
        // if we add another layer in the prefab.
        // We are looking for the 6th parent!
        parentScript = transform.parent.parent.parent.parent.parent.parent.
                       GetComponent<WelderController>();
    }

    void OnTriggerEnter(Collider other) {
        parentScript.UndoMovement();
    }
    void OnTriggerStay(Collider other) {
        parentScript.UndoMovement();
    }
}
