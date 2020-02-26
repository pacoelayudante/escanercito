using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenCvSharp;
using OCVUnity = OpenCvSharp.Unity;
using System.Linq;

class IslaContornos
{
    double area = -1, areaHijos = -1;
    public double Area => contorno == null ? 0 : (area == -1 ? area = Cv2.ContourArea(contorno) : area);
    public double AreaHijos
    {
        get
        {
            if (hijos == null || hijos.Count == 0) return -1;
            if (areaHijos == -1)
            {
                areaHijos = hijos.Select(e => Cv2.ContourArea(e)).Sum();
            }
            return areaHijos;
        }
    }
    public double PorcentajeAreaHijos => Area <= 0 || AreaHijos <= 0 ? 0 : AreaHijos / Area;

    public int indice;
    public Point[][] contornos;
    public HierarchyIndex[] jerarquias;
    public Point[] contorno;
    public List<Point[]> hijos = new List<Point[]>();
    public List<Point[]> hullsHijos = new List<Point[]>();
    public HierarchyIndex jerarq;
    List<int> indicesHijosRecursivo;

    public bool ConHijos => hijos == null ? false : hijos.Count > 0;

    public IslaContornos(int indice, Point[][] contornos, HierarchyIndex[] jerarquias)
    {
        this.indice = indice;
        this.jerarquias = jerarquias;
        this.contornos = contornos;
        jerarq = jerarquias[indice];
        contorno = contornos[indice];

        var indiceHijo = jerarq.Child;
        while (indiceHijo != -1)
        {
            hijos.Add(contornos[indiceHijo]);
            indiceHijo = jerarquias[indiceHijo].Next;
        }
    }
    public void GeneararHijosHulls()
    {
        hullsHijos = hijos.Select(e => Cv2.ConvexHull(e)).ToList();
    }
}