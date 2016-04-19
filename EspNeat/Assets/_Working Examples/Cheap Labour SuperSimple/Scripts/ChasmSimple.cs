using UnityEngine;
using System.Collections;

public class ChasmSimple : MonoBehaviour {
  void OnTriggerEnter(Collider other) {
    WorkerSimpleController controller = other.GetComponent<WorkerSimpleController>();
    //This seems unnecessarily involved, but otherwise there is an error:
    //"Object reference not set to an instance of an object"
    if (controller) {
      controller.OnChasm();
    }
  }
}
