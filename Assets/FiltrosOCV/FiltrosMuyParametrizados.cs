using UnityEngine;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using OpenCvSharp;

[System.Serializable]
public class FiltroAdaptativo
{
    public double maxValue = 255d;
    public AdaptiveThresholdTypes adaptiveMethod = AdaptiveThresholdTypes.MeanC;
    public ThresholdTypes thresholdType = ThresholdTypes.Binary;
    [Guazu.DrawersCopados.SoloImpares]
    public int blockSize = 3;
    public double C = 0;

    public Mat Procesar(Mat entra, Mat sale = null)
    {
        if (sale == null) sale = entra;
        Cv2.AdaptiveThreshold(entra, sale, maxValue, adaptiveMethod, thresholdType, blockSize, C);
        return sale;
    }
}

[System.Serializable]
public class FiltroContornos
{
    public RetrievalModes mode = RetrievalModes.External;
    public ContourApproximationModes method = ContourApproximationModes.ApproxSimple;
    public Point offset = new Point();
    Point[][] salida;
    public Point[][] Salida => salida;
    HierarchyIndex[] jerarquia;
    public HierarchyIndex[] Jerarquia => jerarquia;

    public void Procesar(Mat entra, out Mat[] sale, Mat jerarquia) {
        Cv2.FindContours(entra,out sale,jerarquia,mode,method,offset);
    }
    public Point[][] Procesar(Mat entra)
    {
        if (mode == RetrievalModes.Tree) Debug.Log("Se espera usar el metodo en que se pasan arrays para esto");
        Cv2.FindContours(entra, out salida, out jerarquia, mode, method, offset);
        return salida;
    }
    public void Procesar(Mat entra, out Point[][] salida, out HierarchyIndex[] jerarquia)
    {
        if (mode != RetrievalModes.Tree) Debug.Log("Se espera usar el metodo con un solo parametro para este caso");
        Cv2.FindContours(entra, out salida, out jerarquia, mode, method, offset);
        this.salida = salida;
        this.jerarquia = jerarquia;
    }
    public ArbolDeContornos ProcesarYGenerarArbol(Mat entra)
    {
        if (mode != RetrievalModes.Tree) Debug.Log("Se espera usar el metodo con un solo parametro para este caso");
        Cv2.FindContours(entra, out salida, out jerarquia, mode, method, offset);
        return new ArbolDeContornos(salida, jerarquia);
    }
}

public class ArbolDeContornos
{
    Point[][] todosLosContornos;
    HierarchyIndex[] todasLasJerarquias;
    public List<Contorno> contornosExteriores = new List<Contorno>();

    public Contorno this[int indice] => contornosExteriores[indice];
    public int Count => contornosExteriores.Count;

    public ArbolDeContornos(Point[][] contornos, HierarchyIndex[] jerarquias)
    {
        todosLosContornos = contornos;
        todasLasJerarquias = jerarquias;

        for (int i = 0; i < jerarquias.Length; i++)
        {
            if (jerarquias[i].Parent == -1)
            {
                contornosExteriores.Add(new Contorno(0, i, contornos, jerarquias));
            }
        }
    }

    public IEnumerable<Contorno> ListaAllanada()
    {
        return ListaAllanada(contornosExteriores);
    }
    IEnumerable<Contorno> ListaAllanada(IEnumerable<Contorno> lista)
    {
        return lista.SelectMany(cont => ListaAllanada(cont.hijos)).Concat(lista);
    }
}
public class Contorno
{
    public int profundidad;
    public int indiceOriginal;
    public Point[] contorno;
    public HierarchyIndex jerarquia;
    public Contorno padre;
    public List<Contorno> hijos = new List<Contorno>();

    double area = -1;
    public double Area
    {
        get
        {
            if (area == -1) area = Cv2.ContourArea(contorno);
            return area;
        }
    }

    RotatedRect rotatedRect = new RotatedRect(new Point(), new Size2f(-1, -1), -1);
    public RotatedRect RotatedRect => (rotatedRect.Size.Width < 0) ? rotatedRect = Cv2.MinAreaRect(contorno) : rotatedRect;

    OpenCvSharp.Rect boundingRect = new OpenCvSharp.Rect(new Point(), new Size(-1, -1));
    public OpenCvSharp.Rect BoundingRect => (boundingRect.Size.Width < 0) ? boundingRect = Cv2.BoundingRect(contorno) : boundingRect;


    public Contorno(int indice, Point[][] contornos)
    {
        this.indiceOriginal = indice;
        this.contorno = contornos[indice];
    }
    public Contorno(int profundidad, int indice, Point[][] contornos, HierarchyIndex[] jerarquias, Contorno padre = null)
    {
        this.profundidad = profundidad;
        this.indiceOriginal = indice;
        this.padre = padre;
        contorno = contornos[indice];
        jerarquia = jerarquias[indice];
        var indiceHijo = jerarquia.Child;
        while (indiceHijo != -1)
        {
            hijos.Add(new Contorno(profundidad + 1, indiceHijo, contornos, jerarquias, this));
            indiceHijo = jerarquias[indiceHijo].Next;
        }
    }
    public void Escalar(double escala)
    {
        if(escala==1) return;
        rotatedRect = new RotatedRect(new Point(), new Size2f(-1, -1), -1);
        area = -1;
        contorno = contorno.Select(p => p * escala).ToArray();
    }
}