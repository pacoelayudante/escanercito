using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using System.Linq;
using Mathd = System.Math;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Recuadro
{
    float anchoSupuesto, altoSupuesto;
    // Contorno contornoOriginal;
    Point2f[] verticesNormalizados;
    public Point[] quadReducido;
    public Mat matRecuadroNormalizado;
    public Point2f escalaContornos = new Point2f(1, 1);

    // public Recuadro(Contorno contorno, Point[] quadReducido, Point2f escalaContornos = default(Point2f))
    public Recuadro(Point[] quadReducido, Point2f escalaContornos = default(Point2f))
    {
        UnityEngine.Assertions.Assert.IsTrue(quadReducido.Length == 4);
        this.quadReducido = quadReducido;
        // this.contornoOriginal = contorno;
        if (escalaContornos.X > 0 && escalaContornos.Y > 0) this.escalaContornos = escalaContornos;

        if (quadReducido.Length == 4)
        {
            anchoSupuesto = Mathf.Max(
                (float)quadReducido[0].DistanceTo(quadReducido[1])
                , (float)quadReducido[2].DistanceTo(quadReducido[3]));
            altoSupuesto = (float)Mathf.Max(
                (float)quadReducido[1].DistanceTo(quadReducido[2])
                , (float)quadReducido[3].DistanceTo(quadReducido[0]));
            anchoSupuesto /= escalaContornos.X;
            altoSupuesto /= escalaContornos.Y;

            verticesNormalizados = new Point2f[] {
                    new Point2f(0,0),new Point2f(anchoSupuesto,0),
                    new Point2f(anchoSupuesto,altoSupuesto),new Point2f(0,altoSupuesto),
                };
        }
    }

    public Mat Normalizar(Mat origen, double maxTamLado = 0f)
    {
        var escalaSalida = 1d;
        if (maxTamLado > 0f)
        {
            escalaSalida = maxTamLado / Mathd.Max(anchoSupuesto, altoSupuesto);
            if (escalaSalida > 1) escalaSalida = 1;
        }
        if (origen == null) return null;
        var tam = new Size(anchoSupuesto * escalaSalida, altoSupuesto * escalaSalida);
        var vertsIn = quadReducido.Select(e => new Point2f(e.X / escalaContornos.X, e.Y / escalaContornos.Y)).ToArray();
        var vertsOut = verticesNormalizados;

        if (escalaSalida < 1d) vertsOut = vertsOut.Select(e => e * escalaSalida).ToArray();
        matRecuadroNormalizado = new Mat(tam, origen.Type());

        var transform = Cv2.GetPerspectiveTransform(vertsIn, vertsOut);
        Cv2.WarpPerspective(origen, matRecuadroNormalizado, transform, tam, InterpolationFlags.Cubic, BorderTypes.Constant, null);

        return matRecuadroNormalizado;

    }


}