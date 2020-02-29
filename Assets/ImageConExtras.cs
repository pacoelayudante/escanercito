using UnityEngine;
using UnityEngine.UI;

public class ImageConExtras : RawImage
{
    public bool shouldPreserveAspect = true;
    public bool rotado;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        Texture tex = mainTexture;
        vh.Clear();
        if (tex != null)
        {
            var r = GetPixelAdjustedRect();
            if (shouldPreserveAspect)
            {
                var tamTex = new Vector2(rotado ? tex.height : tex.width, rotado ? tex.width : tex.height);
                PreserveSpriteAspectRatio(ref r, tamTex);
            }
            var v = new Vector4(r.x, r.y, r.x + r.width, r.y + r.height);
            var scaleX = tex.width * tex.texelSize.x;
            var scaleY = tex.height * tex.texelSize.y;

            var uvs = new[]{
                new Vector2(uvRect.xMin * scaleX, uvRect.yMin * scaleY),
                new Vector2(uvRect.xMin * scaleX, uvRect.yMax * scaleY),
                new Vector2(uvRect.xMax * scaleX, uvRect.yMax * scaleY),
                new Vector2(uvRect.xMax * scaleX, uvRect.yMin * scaleY)
            };

            if (rotado)
            {
                var ultimoUV = uvs[3];
                uvs[3] = uvs[2];
                uvs[2] = uvs[1];
                uvs[1] = uvs[0];
                uvs[0] = ultimoUV;
            }

            {
                var color32 = color;
                vh.AddVert(new Vector3(v.x, v.y), color32, uvs[0]);
                vh.AddVert(new Vector3(v.x, v.w), color32, uvs[1]);
                vh.AddVert(new Vector3(v.z, v.w), color32, uvs[2]);
                vh.AddVert(new Vector3(v.z, v.y), color32, uvs[3]);

                vh.AddTriangle(0, 1, 2);
                vh.AddTriangle(2, 3, 0);
            }
        }
    }

    private void PreserveSpriteAspectRatio(ref Rect rect, Vector2 spriteSize)
    {
        var spriteRatio = spriteSize.x / spriteSize.y;
        var rectRatio = rect.width / rect.height;

        if (spriteRatio > rectRatio)
        {
            var oldHeight = rect.height;
            rect.height = rect.width * (1.0f / spriteRatio);
            rect.y += (oldHeight - rect.height) * rectTransform.pivot.y;
        }
        else
        {
            var oldWidth = rect.width;
            rect.width = rect.height * spriteRatio;
            rect.x += (oldWidth - rect.width) * rectTransform.pivot.x;
        }
    }
}