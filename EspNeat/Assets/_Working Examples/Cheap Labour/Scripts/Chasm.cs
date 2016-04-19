using UnityEngine;
using System.Collections;

public class Chasm : MonoBehaviour {
  void OnTriggerEnter(Collider other) {
    WorkerController controller = other.GetComponent<WorkerController>();
    //This seems unnecessarily involved, but otherwise there is an error:
    //"Object reference not set to an instance of an object"
    if (controller) {
      controller.OnChasm();
    }
  }
}
