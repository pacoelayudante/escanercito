using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using OCVUnity = OpenCvSharp.Unity;
using OCVRect = OpenCvSharp.Rect;
using Rect = UnityEngine.Rect;
using System.Linq;
using Mathd = System.Math;
using Guazu.DrawersCopados;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class GeneradorSprites {
    readonly static Vector2[] quadVertex = new Vector2[]{
        new Vector2(-.5f,-.5f),new Vector2(.5f,-.5f),
        new Vector2(-.5f,.5f),new Vector2(.5f,.5f),
        };
        readonly static ushort[] quadTris = new ushort[]{
            0,1,2, 1,3,2
        };

    public static Sprite GenerarSprite(Texture2D texturaOriginal, float rotacion=0, Vector2[] vertices=null) {
        var tamTextura = new Vector2(texturaOriginal.width,texturaOriginal.height);
        var rotQuad = Quaternion.Euler(0,0,rotacion);
        
        if (vertices == null) {
            vertices = quadVertex.Select(v=>(Vector2)(rotQuad* Vector2.Scale(v,tamTextura))).ToArray();
        }
        var tris = quadTris;

        var salida = Sprite.Create(texturaOriginal, new Rect(0,0,texturaOriginal.width,texturaOriginal.height),Vector2.one*0.5f,100,0,SpriteMeshType.Tight);
        Debug.Log(salida.rect.size);
        salida.OverrideGeometry(vertices,tris);
        return salida;
    }
}