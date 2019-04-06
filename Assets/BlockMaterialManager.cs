using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockMaterialManager : MonoBehaviour
{
    public List<Material> p_mats;
    public static List<Material> mats;

    private void Awake()
    {
        mats = p_mats;
    }
}
