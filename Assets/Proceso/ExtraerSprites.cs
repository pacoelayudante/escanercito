using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using OCVUnity = OpenCvSharp.Unity;
using System.Linq;
using Mathd = System.Math;
using Guazu.DrawersCopados;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ExtraerSprites : MonoBehaviour
{
    public ProcesarRecuadros procesarRecuadros;
    public bool conservarOriginal = true;
    [Min(1)]
    public int maxTamLadoImagenProcesada = 512;
    public FiltroAdaptativo filtroAdaptativo = new FiltroAdaptativo() { blockSize = 5, C = 3, thresholdType = ThresholdTypes.BinaryInv };
    [SoloImpares]
    public int tamKernelDilate = 3;
    public int repeatDilate = 2;
    [SoloImpares]
    public int tamMedianBlur = 5;
    public FiltroContornos filtroContornos = new FiltroContornos() { mode = RetrievalModes.Tree, method = ContourApproximationModes.ApproxTC89KCOS };
    public float tamMinimoSprite = 0.45f;
    public List<Texture2D> texturasResultantes = new List<Texture2D>();

    List<Contorno> contornos;
    Mat matRecuadro;
    // List<Contorno> contornosDeSprites;
    // public List<Contorno> ContornosDeSprites => contornosDeSprites;

    public void Extraer()
    {
        Extraer(procesarRecuadros);
    }
    public void Extraer(ProcesarRecuadros procesadorRecuadros, int[] indicesRecuadros = null)
    {
        // indicesRecuadros == 0 >> PROCESAR TODOS
        if (procesadorRecuadros == null) return;

        var recuadros = procesadorRecuadros.Recuadros;
        if (recuadros == null || recuadros.Count == 0) return;
        Extraer(procesarRecuadros.MatOriginal, recuadros[0]);
    }
    public void Extraer(Mat matOriginal, Recuadro recuadro)
    {
        if (recuadro.matRecuadroNormalizado == null)
        {
            recuadro.Normalizar(matOriginal, 0);
        }
        var matRecuadro = recuadro.matRecuadroNormalizado.Clone();
        var escalaSalida = maxTamLadoImagenProcesada / Mathd.Max((double)matRecuadro.Width, (double)matRecuadro.Height);
        if (escalaSalida < 1)
        {
            Cv2.Resize(matRecuadro, matRecuadro, new Size(), escalaSalida, escalaSalida);
        }
        else if (escalaSalida > 1) escalaSalida = 1;
        if (conservarOriginal) matRecuadro = matRecuadro.Clone();
        Cv2.CvtColor(matRecuadro, matRecuadro, ColorConversionCodes.BGR2GRAY);
        matRecuadro = filtroAdaptativo.Procesar(matRecuadro);

        if (repeatDilate > 0)
        {
            var kernelDilate = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(tamKernelDilate, tamKernelDilate));
            Cv2.Dilate(matRecuadro, matRecuadro, kernelDilate, null, repeatDilate);
        }
        if (tamMedianBlur > 1) Cv2.MedianBlur(matRecuadro, matRecuadro, tamMedianBlur);

        var contornosSinProcesar = filtroContornos.Procesar(matRecuadro);
        texturasResultantes.Clear();
        contornos = new List<Contorno>();
        
        var tamRecuadro = recuadro.matRecuadroNormalizado.Size();
        var tamMinimo = tamMinimoSprite*Mathf.Min( tamRecuadro.Width,tamRecuadro.Height);
        for (int i = 0; i < contornosSinProcesar.Length; i++)
        {
            var contniu = new Contorno(i, contornosSinProcesar);
            contniu.Escalar(1d / escalaSalida);
            var bbox = contniu.BoundingRect;
            if (bbox.Width >= tamMinimo && bbox.Height >= tamMinimo
            && bbox.Left > 0 && bbox.Right < tamRecuadro.Width - 1 && bbox.Top > 0 && bbox.Bottom < tamRecuadro.Height - 1)
            {
                texturasResultantes.Add(ExtraerSprite(recuadro.matRecuadroNormalizado, contniu));
                contornos.Add(contniu);
            }
        }

        this.matRecuadro = matRecuadro;
    }

    public Texture2D ExtraerSprite(Mat matOriginal, Contorno contorno)
    {
        var matTexturaAlfa = new Mat(contorno.BoundingRect.Height, contorno.BoundingRect.Width, MatType.CV_8UC1, new Scalar());
        Cv2.DrawContours(matTexturaAlfa, new[] { contorno.contorno }, 0, ProcesarRecuadros.ColEscalarBlanco,
        -1, LineTypes.AntiAlias, null, 0, -contorno.BoundingRect.TopLeft);

        var matTexturaColor = new Mat(matOriginal, contorno.BoundingRect);

        var textura = new Texture2D(matTexturaAlfa.Width, matTexturaAlfa.Height);
        textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;

        Cv2.Merge(matTexturaColor.Split().Concat(new[] { matTexturaAlfa }).ToArray(), matTexturaAlfa);

        textura = OCVUnity.MatToTexture(matTexturaAlfa, textura);

        return textura;
    }

#if UNITY_EDITOR
    UnityEngine.UI.RawImage rawImgPrueba;
    [CustomEditor(typeof(ExtraerSprites))]
    public class ExtraerSpritesEditor : Editor
    {
        bool mostrarAlfa = false;
        public override void OnInspectorGUI()
        {
            var coso = target as ExtraerSprites;
            EditorGUI.BeginChangeCheck();
            var rawImgPrueba = EditorGUILayout.ObjectField("Salida De Prueba", coso.rawImgPrueba, typeof(UnityEngine.UI.RawImage), true) as UnityEngine.UI.RawImage;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(coso, "Coso");
                coso.rawImgPrueba = rawImgPrueba;
            }
            EditorGUI.BeginDisabledGroup(!coso.rawImgPrueba || coso.rawImgPrueba.gameObject.scene.rootCount == 0);
            {
                mostrarAlfa = EditorGUILayout.Toggle("Mostrar Alfa", mostrarAlfa);
                if (GUILayout.Button("Probar"))
                {
                    coso.Extraer();
                    var matdraw = coso.matRecuadro;
                    if (matdraw != null)
                    {
                        if (!mostrarAlfa)
                        {
                            matdraw = coso.procesarRecuadros.Recuadros[0].matRecuadroNormalizado.Clone();
                            //matdraw.SetTo(new Scalar(0));
                            for (int i = 0; i < coso.contornos.Count; i++)
                            {
                                Cv2.DrawContours(matdraw, coso.contornos.Select(c => c.contorno), i, Scalar.RandomColor(), 7);
                                Cv2.Polylines(matdraw, new[] { coso.contornos[i].BoundingRect.ToArray() }, true, Scalar.RandomColor(), 5);
                            }
                        }

                            var textura = new Texture2D(matdraw.Width, matdraw.Height);
                            textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                            OCVUnity.MatToTexture(matdraw, textura);
                            coso.rawImgPrueba.texture = textura;
                            coso.rawImgPrueba.SetNativeSize();
                    }
                }
            }
            EditorGUI.EndDisabledGroup();
            DrawDefaultInspector();
        }
    }
#endif
}
