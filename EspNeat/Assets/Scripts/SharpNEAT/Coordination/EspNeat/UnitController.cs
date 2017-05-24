using UnityEngine;

public abstract class UnitController : MonoBehaviour {
	/// <summary>
	/// This variable will be set to "true" if the unit is chosen during manual evolution
	/// Note there is no need to set it back to "false" at the end of the generation since
	/// the unit will be destroyed any way
	/// </summary>
	private bool selected = false;

    public abstract void Activate(SharpNeat.Phenomes.IBlackBox box);
	
	public abstract void Stop();
	
	public abstract float GetFitness();

	//Used in Optimizer --> DestroyBest and for the manual selection process
    public abstract SharpNeat.Phenomes.IBlackBox GetBox();

	public bool Selected
	{
    get { return selected; }
		set { selected = value; }
	}
}