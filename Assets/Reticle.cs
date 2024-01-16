using System;
using System.Collections.Generic;
using UnityEngine;

public class Reticle : MonoBehaviour
{
    public List<GameObject> rotatingLayers;
    public List<float> rotationRates;
    
    private readonly List<Renderer> _renderers = new();
    
    private void Awake()
    {
        CacheRenderers(transform);
    }

    private void CacheRenderers(Transform root)
    {
        var foundRenderer = root.GetComponent<Renderer>();
        if (foundRenderer != null) _renderers.Add(foundRenderer);

        foreach (Transform child in root) CacheRenderers(child);
    }

    private void Update()
    {
        var limit = Math.Min(rotatingLayers.Count, rotationRates.Count);
        for (var i = 0; i < limit; i++)
        {
            var transformLocalEulerAngles = rotatingLayers[i].transform.localEulerAngles;
            transformLocalEulerAngles.y += rotationRates[i] * Time.deltaTime;
            rotatingLayers[i].transform.localEulerAngles = transformLocalEulerAngles;
        }
    }

    public void SetOpacity(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        
        foreach (var cachedRenderer in _renderers)
        {
            var mat = cachedRenderer.material;
            if (!mat) continue;
            
            var color = mat.color;
            color.a = alpha;
            mat.color = color;
        }
    }
}
