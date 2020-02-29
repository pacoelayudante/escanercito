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

public class ProcesarRecuadros : MonoBehaviour
{
    public readonly static Scalar ColEscalarNegro = new Scalar();
    public readonly static Scalar ColEscalarBlanco = new Scalar(255, 255, 255);
    public readonly static Scalar ColEscalarRojo = new Scalar(0, 0, 255);
    public readonly static Scalar ColEscalarVerde = new Scalar(0, 255, 0);
    public readonly static Scalar ColEscalarAzul = new Scalar(255, 0, 0);

    public bool conservarMatOriginal = true, conservarEscalado = true;
    [Min(1)]
    public int maxTamLadoImagenProcesada = 512;
    public enum EscalarOLimitar { EscalarProporcional, LimitarCadaEje }
    public enum OrdenAchicarDecolorar { AchicarYDecolorar, DecolorarYAchicar }
    public OrdenAchicarDecolorar ordenDeAchicarDecolorar = OrdenAchicarDecolorar.AchicarYDecolorar;
    public EscalarOLimitar escalarOLimitar = EscalarOLimitar.EscalarProporcional;

    public FiltroAdaptativo filtroAdaptativo = new FiltroAdaptativo() { blockSize = 5, C = 3, thresholdType = ThresholdTypes.BinaryInv };
    public FiltroContornos filtroContornos = new FiltroContornos() { mode = RetrievalModes.Tree, method = ContourApproximationModes.ApproxTC89KCOS };
    [Tooltip("El area de todos los hijos de un contorno tiene que ser casi la misma que el area del contorno en si")]
    [Range(0, 1)]
    public double porpMargenDeRecuadro = .85d;
    [Tooltip("Convirtiendo un contorno en RotatedRect, el lado mas chico tiene que superar este valor")]
    public double tamMinimoContornoRecuadro = 25;
    [Tooltip("Diferencia angular maxima entre segmentos para ser considerados que son iguales")]
    public float toleranciaLineaRecta = 15f;

    ArbolDeContornos mapaDeContornos;
    // List<Contorno> contornosRecuadrables;
    // public List<Contorno> ContornosRecuadrables => contornosRecuadrables;
    List<Recuadro> recuadros;
    public List<Recuadro> Recuadros => recuadros;
    Texture ultimaTexturaProcesada;
    Mat matOriginal, matProcesado, matEscalado;
    List<ProtoRecuadro> protoRecuadros = new List<ProtoRecuadro>();
    [Header("Proto recuadros")]
    public int cantCuadrosCargaProtoRecuadro = 30;
    public float factorDescargaProtoRecuadro = 0.25f;
    public float diametroCargadorProtoRecuadro = 20f;
    public float maxDiferenciaProtoRecuadros = .1f;
    bool autoDetectados = false;
    public bool AutoDetectados => autoDetectados;

    class ProtoRecuadro
    {
        public float carga = 0f;
        public OpenCvSharp.Rect boundingBox;
        public Point[] quad;

        float area;
        public ProtoRecuadro(Point[] contorno)
        {
            this.quad = contorno;
            this.boundingBox = Cv2.BoundingRect(contorno);
            area = this.boundingBox.Width * this.boundingBox.Height;
        }

        public bool Comparar(Contorno contorno, float umbralDiferenciaArea = 0.9f)
        {
            return Comparar(contorno.contorno, umbralDiferenciaArea);
        }
        public bool Comparar(Point[] contorno, float umbralDiferenciaArea = 0.9f)
        {
            var entreBoundingRect = Cv2.BoundingRect(contorno);
            var areaEntra = entreBoundingRect.Width * entreBoundingRect.Height;
            var areaMin = Mathf.Min(areaEntra, area);
            var interseccion = entreBoundingRect.Intersect(boundingBox);
            if (Mathf.Abs(areaMin - interseccion.Width * interseccion.Height) < areaMin * umbralDiferenciaArea)
            {
                boundingBox = entreBoundingRect;
                area = areaEntra;
                return true;
            }
            return false;
        }
    }

    public float ConstanteAdaptiva
    {
        get => (float)filtroAdaptativo.C;
        set => filtroAdaptativo.C = value;
    }
    public float TamBloqueAdaptivo
    {
        get => (float)(filtroAdaptativo.blockSize - 1) / 2;
        set => filtroAdaptativo.blockSize = ((int)value) * 2 + 1;
    }

    public Mat MatOriginal
    {
        get
        {
            if (matOriginal != null) return matOriginal;
            else if (ultimaTexturaProcesada)
            {
                var tex2d = ultimaTexturaProcesada as Texture2D;
                if (tex2d) return OCVUnity.TextureToMat(tex2d);
                var webtex = ultimaTexturaProcesada as WebCamTexture;
                if (webtex) return OCVUnity.TextureToMat(webtex);
            }
            return null;
        }
    }
    public Mat MatOriginalEscalado
    {
        get
        {
            if (matEscalado != null) return matEscalado;
            else if (ultimaTexturaProcesada)
            {
                var matOrig = MatOriginal;
                if (matOrig == null) return null;
                return EscalarYDesaturarMat(MatOriginal, new Mat(), false);
            }
            return null;
        }
    }

    public Mat ProcesarTextura(WebCamTexture texturaEntrada)
    {
        if (texturaEntrada == null) return null;
        ultimaTexturaProcesada = texturaEntrada;
        return ProcesarTextura(OCVUnity.TextureToMat(texturaEntrada));
    }
    public Mat ProcesarTextura(Texture2D texturaEntrada)
    {
        if (texturaEntrada == null) return null;
        ultimaTexturaProcesada = texturaEntrada;
        return ProcesarTextura(OCVUnity.TextureToMat(texturaEntrada));
    }
    public Mat ProcesarTextura(Mat matEntrada)
    {
        if (conservarMatOriginal) matOriginal = matEntrada.Clone();
        else matOriginal = null;
        matProcesado = matEntrada;

        var escalaXY = new Point2f(matProcesado.Width, matProcesado.Height);
        matProcesado = EscalarYDesaturarMat(matProcesado);
        escalaXY.X = matProcesado.Width / escalaXY.X;
        escalaXY.Y = matProcesado.Height / escalaXY.Y;
        filtroAdaptativo.Procesar(matProcesado);

        // if(protoRecuadros.Count>0) {
        //     recuadros = protoRecuadros.Select(pr => new Recuadro(pr.quad, escalaXY)).ToList();
        //     protoRecuadros.Clear();
        //     return matProcesado;
        // }
        protoRecuadros.Clear();

        mapaDeContornos = filtroContornos.ProcesarYGenerarArbol(matProcesado);
        // contornosRecuadrables = FiltrarContornos(mapaDeContornos.contornosExteriores, (cont) =>
        //      cont.hijos.Count > 0 && cont.RotatedRect.Size.Width >= tamMinimoContornoRecuadro &&
        //      cont.RotatedRect.Size.Height >= tamMinimoContornoRecuadro).SelectMany(c => c.hijos).ToList();

        recuadros = FiltrarContornos(mapaDeContornos.contornosExteriores, (cont) =>
             cont.hijos.Count > 0 && cont.RotatedRect.Size.Width >= tamMinimoContornoRecuadro &&
             cont.RotatedRect.Size.Height >= tamMinimoContornoRecuadro).SelectMany(c => c.hijos).
             Where(cont => cont.RotatedRect.Size.Width >= tamMinimoContornoRecuadro &&
             cont.RotatedRect.Size.Height >= tamMinimoContornoRecuadro).Select(SimplificarContorno).Where(cont => cont.Length == 4)
             .Select(quad => new Recuadro(quad, escalaXY)).ToList();
            // .Select(cont => new Recuadro(cont, Simplificar(cont), escalaXY)).ToList();

        return matProcesado;
    }
    public Point[] SimplificarContorno(Contorno contorno)
    {
        var epsilonTalVez = Mathd.Min(contorno.BoundingRect.Width, contorno.BoundingRect.Height) / 10d;
        return Cv2.ApproxPolyDP(contorno.contorno, epsilonTalVez, true);
    }

    public Texture2D PreProcesarTextura(Texture2D texturaEntrada, Texture2D salida)
    {
        return PreProcesarTextura(OCVUnity.TextureToMat(texturaEntrada), salida);
    }
    public Texture2D PreProcesarTextura(WebCamTexture texturaEntrada, Texture2D salida)
    {
        return PreProcesarTextura(OCVUnity.TextureToMat(texturaEntrada), salida);
    }
    public Texture2D PreProcesarTextura(Mat matEntrada, Texture2D text2d)
    {
        matEntrada = EscalarYDesaturarMat(matEntrada);
        filtroAdaptativo.Procesar(matEntrada);
        // Mat[] matContornos = null;
        // Mat jerarquia = new Mat();
        // filtroContornos.Procesar(matEntrada,out matContornos,jerarquia);
        var mapaDeContornos = filtroContornos.ProcesarYGenerarArbol(matEntrada);
        var contornos = FiltrarContornos(mapaDeContornos.contornosExteriores, (cont) =>
             cont.hijos.Count > 0 && cont.RotatedRect.Size.Width >= tamMinimoContornoRecuadro &&
             cont.RotatedRect.Size.Height >= tamMinimoContornoRecuadro).SelectMany(c => c.hijos).Where(cont =>
            cont.RotatedRect.Size.Width >= tamMinimoContornoRecuadro &&
             cont.RotatedRect.Size.Height >= tamMinimoContornoRecuadro);//.Select(c=>c.contorno).ToList();
        Cv2.CvtColor(matEntrada, matEntrada, ColorConversionCodes.GRAY2BGR);
        // Cv2.DrawContours(matEntrada,matContornos,-1,ColEscalarRojo,-1);
        // Cv2.DrawContours(matEntrada, contornos.Select(c => c.contorno), -1, ColEscalarRojo, 1);

        var contornosReducidos = contornos.Select(SimplificarContorno);
        // Cv2.DrawContours(matEntrada, contornosReducidos, -1, ColEscalarAzul, 1);
        contornosReducidos = contornosReducidos.Where(cont => cont.Length == 4);
        Cv2.DrawContours(matEntrada, contornosReducidos, -1, ColEscalarVerde, 3);

        autoDetectados = false;
        foreach (var contorno in contornosReducidos)
        {
            var coincidencia = protoRecuadros.FirstOrDefault(proto => proto.Comparar(contorno,maxDiferenciaProtoRecuadros));
            if (coincidencia == null)
            {
                coincidencia = new ProtoRecuadro(contorno);
                protoRecuadros.Add(coincidencia);
            }
            if(coincidencia.carga<1.5f)coincidencia.carga += (1f+factorDescargaProtoRecuadro)/cantCuadrosCargaProtoRecuadro;
        }
        foreach(var protoRecuadro in protoRecuadros) {
            // Cv2.Rectangle(matEntrada,protoRecuadro.boundingBox.TopLeft,protoRecuadro.boundingBox.BottomRight, ColEscalarVerde, 2);
            Cv2.Ellipse(matEntrada,protoRecuadro.boundingBox.Center,new Size(diametroCargadorProtoRecuadro,diametroCargadorProtoRecuadro),0,0,360f*protoRecuadro.carga,ColEscalarVerde,-1);
            protoRecuadro.carga -= factorDescargaProtoRecuadro/cantCuadrosCargaProtoRecuadro;
        }
        for (int i=protoRecuadros.Count-1; i>=0; i--) {
            if(protoRecuadros[i].carga <= 0f) protoRecuadros.RemoveAt(i);
        }
        if (protoRecuadros.Count==0) autoDetectados = false;
        else autoDetectados = protoRecuadros.All(protoRecuadro=>protoRecuadro.carga>=1f-factorDescargaProtoRecuadro/cantCuadrosCargaProtoRecuadro);

        if (text2d == null || !text2d.isReadable)
        {
            text2d = new Texture2D(matEntrada.Width, matEntrada.Height);
            text2d.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        }
        OCVUnity.MatToTexture(matEntrada, text2d);
        matEntrada.Dispose();
        mapaDeContornos = null;
        contornos = null;
        System.GC.Collect();
        // foreach(var m in matContornos) m.Dispose();
        // jerarquia.Dispose();
        return text2d;
    }

    public Mat EscalarYDesaturarMat(Mat matIn, Mat matOut = null, bool convertirColor = true, ColorConversionCodes procesoDeColor = ColorConversionCodes.BGR2GRAY)
    {
        if (matOut == null) matOut = matIn;

        var escala = maxTamLadoImagenProcesada / Mathd.Max((double)matIn.Width, (double)matIn.Height);
        var tam = new Size(Mathf.Min(maxTamLadoImagenProcesada, matIn.Width), Mathf.Min(maxTamLadoImagenProcesada, matIn.Height));

        if (convertirColor && ordenDeAchicarDecolorar == OrdenAchicarDecolorar.DecolorarYAchicar)
        {
            Cv2.CvtColor(matIn, matOut, procesoDeColor);
            if (escalarOLimitar == EscalarOLimitar.LimitarCadaEje) Cv2.Resize(matOut, matOut, tam);
            else if (escala < 1d) Cv2.Resize(matOut, matOut, new Size(), escala, escala);
            if (conservarEscalado) matEscalado = matOut.Clone();
        }
        else
        {
            if (escalarOLimitar == EscalarOLimitar.LimitarCadaEje) Cv2.Resize(matIn, matOut, tam);
            else if (escala < 1d) Cv2.Resize(matIn, matOut, new Size(), escala, escala);
            if (conservarEscalado) matEscalado = matOut.Clone();
            if (convertirColor) Cv2.CvtColor(matOut, matOut, procesoDeColor);
        }

        return matOut;
    }

    IEnumerable<Contorno> FiltrarContornos(IEnumerable<Contorno> contornos, System.Func<Contorno, bool> whereificar = null)
    {
        //filtro contornos que no quiero ni procesar
        if (whereificar != null) contornos = contornos.Where(whereificar);
        //agrupo en "cumple o no cumpl"
        var grupos = contornos.GroupBy((contorno) => (contorno.hijos.Sum(hijo => hijo.Area) / contorno.Area) >= porpMargenDeRecuadro);
        //los que "no cumplen" , filtro sus hijos (me quedo de los hijos con los que cumplen, y a su vez a los hijos que no cumplen, filtro esos hijos)
        var hijosFiltrados = grupos.Where(grupo => !grupo.Key).SelectMany(g => g).SelectMany(gAgain => FiltrarContornos(gAgain.hijos, whereificar));
        //junto los que cumplen de este nivel con los que cumplen de las descendencias
        return grupos.Where(grupo => grupo.Key).SelectMany(g => g).Concat(hijosFiltrados);
    }

#if UNITY_EDITOR
    Texture2D texturaPrueba;
    UnityEngine.UI.RawImage rawImgPrueba;
    [CustomEditor(typeof(ProcesarRecuadros))]
    public class ProcesarRecuadrosEditor : Editor
    {
        bool verRecs;
        public override void OnInspectorGUI()
        {
            var coso = target as ProcesarRecuadros;
            EditorGUI.BeginChangeCheck();
            var texturaPrueba = EditorGUILayout.ObjectField("Textura De Prueba", coso.texturaPrueba, typeof(Texture2D), true) as Texture2D;
            var rawImgPrueba = EditorGUILayout.ObjectField("Salida De Prueba", coso.rawImgPrueba, typeof(UnityEngine.UI.RawImage), true) as UnityEngine.UI.RawImage;
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(coso, "Coso");
                coso.texturaPrueba = texturaPrueba;
                coso.rawImgPrueba = rawImgPrueba;
            }
            EditorGUI.BeginDisabledGroup(!coso.texturaPrueba || !coso.rawImgPrueba || coso.rawImgPrueba.gameObject.scene.rootCount == 0);
            if (GUILayout.Button("Probar"))
            {
                var matDebug = coso.ProcesarTextura(coso.texturaPrueba);
                var matdraw = matDebug;
                // var matdraw = OCVUnity.TextureToMat(coso.texturaPrueba);
                var escala = matdraw.Width / (double)matDebug.Width;
                if (matdraw.Channels() == 1) Cv2.CvtColor(matdraw, matdraw, ColorConversionCodes.GRAY2BGR);
                // Cv2.DrawContours(matdraw, coso.contornosRecuadrables.Select(c => c.padre.contorno.Select(p => p * escala)), -1, ProcesarRecuadros.ColEscalarVerde);
                // Cv2.DrawContours(matdraw, coso.contornosRecuadrables.Select(c => c.contorno.Select(p => p * escala)), -1, ProcesarRecuadros.ColEscalarAzul);
                foreach (var rec in coso.recuadros)
                {
                    // rec.DibujarDebug(matdraw, ColEscalarRojo, escala);
                    // var rect = Cv2.MinAreaRect(rec.contornoOriginal);
                    // Cv2.Polylines(matdraw, new[] { rect.Points().Select(e => new Point(e.X, e.Y)) }, true, ColEscalarRojo, 3);
                }

                var textura = new Texture2D(matdraw.Width, matdraw.Height);
                textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                OCVUnity.MatToTexture(matdraw, textura);
                coso.rawImgPrueba.texture = textura;
                coso.rawImgPrueba.SetNativeSize();
            }
            EditorGUI.EndDisabledGroup();
            DrawDefaultInspector();
            // EditorGUI.BeginDisabledGroup(coso.Recuadros == null);
            // if (verRecs = EditorGUILayout.Foldout(verRecs && coso.Recuadros != null, "Ver Recuadros"))
            // {
            // foreach (var rec in coso.Recuadros)
            // {
            // GUILayout.Label($"{rec.indiceContorno} - {rec.escalaContornos.X}x{rec.escalaContornos.Y}\n{rec.anchoSupuesto}x{rec.altoSupuesto}");
            // }
            // }
            // EditorGUI.EndDisabledGroup();
        }
    }
#endif

}