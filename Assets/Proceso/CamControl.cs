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

    public GameObject contenedorSprites;
    public RectTransform padreSprites;

    public bool preprocesarEnVivo = true;
    public ProcesarRecuadros procesarRecuadros;
    public ExtraerSprites sprites;
    public Button verCamVerSprites;

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

        if (verCamVerSprites) {
            verCamVerSprites.onClick.AddListener( ()=>{
                mostrarCamaraAca.enabled = !mostrarCamaraAca.enabled;
                contenedorSprites.gameObject.SetActive(!mostrarCamaraAca.enabled);
            });
        }

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
                if(contenedorSprites) contenedorSprites.gameObject.SetActive(false);
                if(verCamVerSprites) verCamVerSprites.gameObject.SetActive(false);
                camara.Play();
                
                    mostrarCamaraAca.texture = camara;
                    var mostrarConExtras = mostrarCamaraAca as ImageConExtras;
                    if (mostrarConExtras) {
                        mostrarConExtras.rotado = camara.videoRotationAngle != 0;
                    }

                if (textUI) textUI.text += $"\nDevice {camara.deviceName} - {camara.width}x{camara.height}-{camara.videoRotationAngle}°";
                if (preprocesarEnVivo) StartCoroutine(PreprocesarEnVivo());
            }
        }
        else
        {
            Debug.LogError("Sin Permiso :(");
        }
    }

    IEnumerator PreprocesarEnVivo()
    {
        Texture2D textProc = null;
        while (preprocesarEnVivo && camara.isPlaying)
        {
            if (camara.didUpdateThisFrame)
            {
                mostrarCamaraAca.texture = (textProc = procesarRecuadros.PreProcesarTextura(camara, textProc));
                if (procesarRecuadros.AutoDetectados) AlTocarBoton();
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
            // var coso = procesarRecuadros;
            var matDebug = procesarRecuadros.ProcesarTextura(camara);
            // var matdraw = matDebug;
            var matdraw = OpenCvSharp.Unity.TextureToMat(camara);
            if (sprites) sprites.Extraer();

            camara.Stop();

            var escala = matdraw.Width / (double)matDebug.Width;
            
            OpenCvSharp.Cv2.DrawContours(matdraw, procesarRecuadros.Recuadros.Select(rec => rec.quadReducido.Select(p => p * escala)),
            -1, ProcesarRecuadros.ColEscalarAzul, 3);

            if (mostrarCamaraAca)
            {
                var textura = new Texture2D(matdraw.Width, matdraw.Height);
                textura.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                OpenCvSharp.Unity.MatToTexture(matdraw, textura);

                mostrarCamaraAca.texture = textura;
                if(contenedorSprites && padreSprites)MostrarSprites();
                else StartCoroutine(SlideShow(textura));
            }
        }
    }

    void MostrarSprites() {
        if (contenedorSprites && padreSprites) {
            if(verCamVerSprites) verCamVerSprites.gameObject.SetActive(true);
            mostrarCamaraAca.enabled = false;
            contenedorSprites.gameObject.SetActive(true);
            var imgs = padreSprites.GetComponentsInChildren< RawImage>();
            foreach(var img in imgs) img.gameObject.SetActive(false);
            for (int i=0; i<sprites.texturasResultantes.Count; i++) {
                RawImage img = i<imgs.Length?imgs[i]:Instantiate(imgs[0],padreSprites);
                img.gameObject.SetActive(true);
                img.texture = sprites.texturasResultantes[i];
            }
        }
    }

    IEnumerator SlideShow(Texture2D textResultA)
    {
        int indice = -1;
        float aspectActual = aspectFitter ? aspectFitter.aspectRatio : 1f;
        var mostrarConExtras = mostrarCamaraAca as ImageConExtras;
        var rotado = mostrarConExtras && mostrarConExtras.rotado;
        while (!camara.isPlaying)
        {
            indice++;
            if (sprites.texturasResultantes == null || sprites.texturasResultantes.Count == 0) indice = -1;
            else if (indice == sprites.texturasResultantes.Count) indice = -1;
            if (indice == -1)
            {
                if (aspectFitter) aspectFitter.aspectRatio = aspectActual;
                if (mostrarConExtras) mostrarConExtras.rotado = rotado;
                mostrarCamaraAca.texture = textResultA;
            }
            else
            {
                mostrarCamaraAca.texture = sprites.texturasResultantes[indice];
                if (mostrarConExtras) mostrarConExtras.rotado = false;
                if (aspectFitter) aspectFitter.aspectRatio = mostrarCamaraAca.texture.width / (float)mostrarCamaraAca.texture.height;
            }
            yield return new WaitForSeconds(1f);
        }
    }
}
