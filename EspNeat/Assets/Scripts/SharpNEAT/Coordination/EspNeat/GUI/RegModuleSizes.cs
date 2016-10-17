using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace SharpNeat.Coordination
{
    public class RegModuleSizes {
		
        //public List<Vector3> mainTexturePosition = new List<Vector3>();
        public List<Vector2> mainTextureSize = new List<Vector2>();
        //public List<Vector3> draggPosition = new List<Vector3>();
        public List<Vector2> dragSize = new List<Vector2>();
        public List<Vector2> regulationPosition = new List<Vector2>();
        //public List<Vector2> regulationSize = new List<Vector2>();
        //public List<Vector3> addBackgroundPosition = new List<Vector3>();
        public List<Vector2> addBackgroundSize = new List<Vector2>();
        public List<Vector2> addPlusPosition = new List<Vector2>();
        //public List<Vector2> addPlusSize = new List<Vector2>();
        public List<Vector3> moduleOffset = new List<Vector3>();
        public List<Vector3> colliderCenter = new List<Vector3>();
		public List<Vector3> colliderSize = new List<Vector3>();
		public List<Vector2> evolvePosition = new List<Vector2>();
		public List<Vector2> optionsPosition = new List<Vector2>();

        public RegModuleSizes()
        {
            // Values for 0 modules are stored by default in the prefab
			// so this is used for the regulation module representation
			// when it is whitin another module, with children hidden.
            //mainTexturePosition.Add(new Vector2(62.2f, -41.7f));
            mainTextureSize.Add(new Vector2(304.63f, 170f));
            //draggPosition.Add(new Vector2(50.28f, -28.8f));
			dragSize.Add(new Vector2(438.19f, 281.5f));
            regulationPosition.Add(new Vector2(0f, 0f));
            //regulationSize.Add(new Vector2(100f, 100f));
            //addBackgroundPosition.Add(new Vector2(0f, 0f));
            addBackgroundSize.Add(new Vector2(400.7f, 123.8f));
            addPlusPosition.Add(new Vector2(152.1f, -13.38f));
            //addPlusSize.Add(new Vector2(80f, 80f));
            moduleOffset.Add(new Vector3(0f, 0f, 0f));
            colliderCenter.Add(new Vector3(-1.3f, 0f, 1.13f));
			colliderSize.Add(new Vector3(9.17f, 15f, 6.06f));
			evolvePosition.Add(new Vector2(-68.09f, -56f));
			optionsPosition.Add(new Vector2(91.5f, 44.3f));

            // Values for 1 module
            mainTextureSize.Add(new Vector2(420.2f, 291.2f));
            dragSize.Add(new Vector2(445.74f, 313.7f));
            regulationPosition.Add(new Vector2(165.5f, 0f));
            addBackgroundSize.Add(new Vector2(407.7f, 157.6f));
            addPlusPosition.Add(new Vector2(305.9f, -30.4f));
            moduleOffset.Add(new Vector3(-0.2f, 0f, 2.91f));
            colliderCenter.Add(new Vector3(-1.4f, 0f, 1.39f));
			colliderSize.Add(new Vector3(10.11f, 15f, 6.59f));
			evolvePosition.Add(new Vector2(158.48f, -7.7f));
			optionsPosition.Add(new Vector2(199.1f, 44.3f));

            // Values for 2 modules
            mainTextureSize.Add(new Vector2(719.6f, 291.2f));
            dragSize.Add(new Vector2(746.9f, 313.7f));
            regulationPosition.Add(new Vector2(464.8f, 0f));
            addBackgroundSize.Add(new Vector2(706f, 157.6f));
            addPlusPosition.Add(new Vector2(602.3f, -30.4f));
            moduleOffset.Add(new Vector3(-7.1f, 0f, 2.91f));
            colliderCenter.Add(new Vector3(-4.8f, 0f, 1.39f));
			colliderSize.Add(new Vector3(16.37f, 15f, 6.59f));
			evolvePosition.Add(new Vector2(456.83f, -7.7f));
			optionsPosition.Add(new Vector2(500.5f, 44.3f));

            // Values for 3 modules
            mainTextureSize.Add(new Vector2(904f, 396.2f));
            dragSize.Add(new Vector2(929.3f, 421.6f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(890.5f, 262.1f));
            addPlusPosition.Add(new Vector2(408.7f, -161.7f));
            moduleOffset.Add(new Vector3(-14f, 0f, 2.91f));
            colliderCenter.Add(new Vector3(-7f, 0f, 2.76f));
			colliderSize.Add(new Vector3(21.2f, 15f, 9.74f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));

            // Values for 4 modules
            mainTextureSize.Add(new Vector2(904f, 473.4f));
            dragSize.Add(new Vector2(929.3f, 498.2f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(890.5f, 338.2f));
            addPlusPosition.Add(new Vector2(408.7f, -188.6f));
            moduleOffset.Add(new Vector3(-0.2f, 0f, 6.9f));
            colliderCenter.Add(new Vector3(-7f, 0f, 3.58f));
			colliderSize.Add(new Vector3(21.2f, 15f, 11.22f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));

            // Values for 5 modules
            mainTextureSize.Add(new Vector2(904f, 473.4f));
            dragSize.Add(new Vector2(929.3f, 498.2f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(890.5f, 338.2f));
            addPlusPosition.Add(new Vector2(688.27f, -188.6f));
            moduleOffset.Add(new Vector3(-7.1f, 0f, 6.9f));
            colliderCenter.Add(new Vector3(-7f, 0f, 3.58f));
			colliderSize.Add(new Vector3(21.2f, 15f, 11.22f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));

            // Values for 6 modules
            mainTextureSize.Add(new Vector2(904f, 473.4f));
            dragSize.Add(new Vector2(929.3f, 498.2f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(890.5f, 338.2f));
            addPlusPosition.Add(new Vector2(408.7f, -188.6f));
            moduleOffset.Add(new Vector3(-0.2f, 0f, 6.9f));
            colliderCenter.Add(new Vector3(-7f, 0f, 3.58f));
			colliderSize.Add(new Vector3(21.2f, 15f, 11.22f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));

            // Values for 7 modules
            mainTextureSize.Add(new Vector2(719.6f, 291.2f));
            dragSize.Add(new Vector2(746.9f, 313.7f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(706f, 157.6f));
            addPlusPosition.Add(new Vector2(602.3f, -30.4f));
			moduleOffset.Add(new Vector3(-0.2f, 0f, 0f));
			colliderCenter.Add(new Vector3(-7f, 0f, 3.58f));
			colliderSize.Add(new Vector3(21.2f, 15f, 11.22f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));

            // Values for 8 modules
            mainTextureSize.Add(new Vector2(719.6f, 291.2f));
            dragSize.Add(new Vector2(746.9f, 313.7f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(706f, 157.6f));
            addPlusPosition.Add(new Vector2(602.3f, -30.4f));
			moduleOffset.Add(new Vector3(-7.1f, 0f, 0f));
			colliderCenter.Add(new Vector3(-7f, 0f, 3.58f));
			colliderSize.Add(new Vector3(21.2f, 15f, 11.22f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));

            // Values for 9 modules
            mainTextureSize.Add(new Vector2(719.6f, 291.2f));
            dragSize.Add(new Vector2(746.9f, 313.7f));
            regulationPosition.Add(new Vector2(649.3f, 0f));
            addBackgroundSize.Add(new Vector2(706f, 157.6f));
            addPlusPosition.Add(new Vector2(602.3f, -30.4f));
			moduleOffset.Add(new Vector3(-14f, 0f, 0f));
			colliderCenter.Add(new Vector3(-7f, 0f, 3.58f));
			colliderSize.Add(new Vector3(21.2f, 15f, 11.22f));
			evolvePosition.Add(new Vector2(622.9f, -7.7f));
			optionsPosition.Add(new Vector2(664.4f, 44.3f));
        }
    }    
}