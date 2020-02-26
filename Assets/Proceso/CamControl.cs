using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class CamControl : MonoBehaviour
{
    public RawImage mostrarCamaraAca;
    public Button boton;
    public Text textUI;

    public bool preprocesarEnVivo = true;
    public ProcesarRecuadros procesarRecuadros;
    public ExtraerSprites sprites;

    AspectRatioFitter aspectFitter;
    WebCamTexture camara;

    void Start()
    {
        if (!boton) boton = GetComponent<Button>();
        if (!mostrarCamaraAca) mostrarCamaraAca = GetComponent<RawImage>();
        if (mostrarCamaraAca) aspectFitter = GetComponent<AspectRatioFitter>();
        if (!textUI) textUI = GetComponentInChildren<Text>();
        StartCoroutine(IniciarCamara());
        if (boton) boton.onClick.AddListener(AlTocarBoton);
    }
    IEnumerator IniciarCamara()
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            WebCamDevice mejorDevice = WebCamTexture.devices.LastOrDefault();
            if (textUI) textUI.text = $"Device count {WebCamTexture.devices.Length}";
            Resolution mejorResolucion = new Resolution();
            for (int i = 0; i < WebCamTexture.devices.Length; i++)
            {
                var device = WebCamTexture.devices[i];
                if (textUI) textUI.text += $"\n{i}){device.name}";
                if (device.availableResolutions == null) continue;
                if (textUI) textUI.text += $" - resoluciones {device.availableResolutions.Length}";
                for (int j = 0; j < device.availableResolutions.Length; j++)
                {
                    var res = device.availableResolutions[j];
                    if (textUI) textUI.text += $"\n{res}";
                    if (res.height > mejorResolucion.height
                    || (res.height == mejorResolucion.height && res.refreshRate > mejorResolucion.refreshRate))
                    {
                        mejorDevice = device;
                        mejorResolucion = res;
                    }
                }
            }
            Debug.Log($"Device {mejorDevice.name} - {mejorResolucion}");

            camara = new WebCamTexture(mejorDevice.name, mejorResolucion.width, mejorResolucion.height, mejorResolucion.refreshRate);
            if (camara)
            {
                camara.Play();
                mostrarCamaraAca.texture = camara;
                if (mostrarCamaraAca.rectTransform.anchorMax.x != .5f)
                {
                    var rectTam = mostrarCamaraAca.rectTransform.rect.size;
                    mostrarCamaraAca.rectTransform.anchorMax = mostrarCamaraAca.rectTransform.anchorMin = Vector2.one * 0.5f;
                    mostrarCamaraAca.rectTransform.sizeDelta = new Vector2(camara.width, camara.height);

                    var escala = Mathf.Min(rectTam.x / camara.width, rectTam.y / camara.height);
                    if (camara.videoRotationAngle == 90)
                    {
                        mostrarCamaraAca.rectTransform.Rotate(0, 0, -camara.videoRotationAngle);
                        escala = Mathf.Min(rectTam.y / camara.width, rectTam.x / camara.height);
                    }
                    mostrarCamaraAca.rectTransform.localScale = Vector3.one * escala;
                }

                if (textUI) textUI.text += $"\nDevice {camara.deviceName} - {camara.width}x{camara.height}-{camara.videoRotationAngle}°";
                if(preprocesarEnVivo) StartCoroutine(PreprocesarEnVivo());
            }
        }
        else
        {
            Debug.LogError("Sin Permiso :(");
        }
    }

    IEnumerator PreprocesarEnVivo() {
        Texture2D textProc = null;
        while (preprocesarEnVivo && camara.isPlaying) {
            if (camara.didUpdateThisFrame) {
                mostrarCamaraAca.texture = (textProc = procesarRecuadros.PreProcesarTextura(camara,textProc));
            }
            yield return null;
        }
    }

    void AlTocarBoton()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam) || camara == null || !camara.isPlaying)
        {
            StartCoroutine(IniciarCamara());
        }
        else
        {
            var coso = procesarRecuadros;
            var matDebug = coso.ProcesarTextura(camara);
            // void matdraw = matDebug;
            var matdraw = OpenCvSharp.Unity.TextureToMat(camara);
            if (sprites) sprites.Extraer();

            camara.Stop();

            var escala = matdraw.Width / (double)matDebug.Width;
            if (matdraw.Channels() == 1) OpenCvSharp.Cv2.CvtColor(matdraw, matdraw, OpenCvSharp.ColorConversionCodes.GRAY2BGR);
            OpenCvSharp.Cv2.DrawContours(matdraw, coso.ContornosRecuadrables.Select(c => c.padre.contorno.Select(p => p * escala)), -1, ProcesarRecuadros.ColEscalarVerde);
            OpenCvSharp.Cv2.DrawContours(matdraw, coso.ContornosRecuadrables.Select(c => c.contorno.Select(p => p * escala)), -1, ProcesarRecuadros.ColEscalarAzul);
            foreach (var rec in coso.Recuadros) rec.DibujarDebug(matdraw, ProcesarRecuadros.ColEscalarRojo, escala);

            if (mostrarCamaraAca)
            {
                var textura = new Texture2D(matdraw.Width, matdraw.Height);
                textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                OpenCvSharp.Unity.MatToTexture(matdraw, textura);

                mostrarCamaraAca.texture = textura;
                StartCoroutine(SlideShow(textura));
            }
        }
    }

    IEnumerator SlideShow(Texture2D textResultA)
    {
        int indice = -1;
        float aspectActual = aspectFitter?aspectFitter.aspectRatio:1f;
        while (!camara.isPlaying)
        {
            indice++;
            if (sprites.texturasResultantes == null || sprites.texturasResultantes.Count == 0) indice = -1;
            else if (indice == sprites.texturasResultantes.Count) indice = -1;
            if (indice == -1)
            {
                if(aspectFitter)aspectFitter.aspectRatio = aspectActual;
                mostrarCamaraAca.texture = textResultA;
            }
            else
            {
                mostrarCamaraAca.texture = sprites.texturasResultantes[indice];
                if(aspectFitter)aspectFitter.aspectRatio = mostrarCamaraAca.texture.width / (float)mostrarCamaraAca.texture.height;
            }
            yield return new WaitForSeconds(1f);
        }
    }
}
