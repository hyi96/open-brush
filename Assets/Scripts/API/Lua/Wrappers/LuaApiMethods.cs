﻿using System.Collections.Generic;
using System.Linq;
using SVGMeshUnity;
using UnityEngine;

namespace TiltBrush
{
    public static class LuaApiMethods
    {
        public static void DrawPath(List<TrTransform> path)
        {
            DrawStrokes.TrTransformListToStroke(path, Vector3.zero);
        }

        public static void DrawPaths(List<List<TrTransform>> paths)
        {
            DrawStrokes.TrTransformListsToStroke(paths, Vector3.zero);
        }

        public static List<TrTransform> PathFromSvg(string svgPathString)
        {
            // Joins all the paths into one and returns points as TrTransforms
            SVGData svgData = new SVGData();
            svgData.Path(svgPathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            var origin = svgPolyline.Polyline[0][0];
            return svgPolyline.Polyline.SelectMany(l=>l).Select(p => TrTransform.T(p - origin)).ToList();
        }

        public static List<List<TrTransform>> PathsFromSvg(string svgPathString)
        {
            // Joins all the paths into one and returns points as TrTransforms
            SVGData svgData = new SVGData();
            svgData.Path(svgPathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            var origin = svgPolyline.Polyline[0][0];
            return svgPolyline.Polyline.Select(p => p.Select(q => TrTransform.T(q - origin)).ToList()).ToList();
        }

        public static List<TrTransform> TranslatePath(List<TrTransform> path, Vector3 translation)
        {
            return TransformPath(path, TrTransform.T(translation));
        }

        public static List<TrTransform> RotatePath(List<TrTransform> path, Quaternion rotation)
        {
            return TransformPath(path, TrTransform.R(rotation));
        }

        public static List<TrTransform> ScalePath(List<TrTransform> path, Vector3 scale)
        {
            // Supports non-uniform scaling
            return path.Select(tr => TrTransform.TRS(
                new Vector3(
                    tr.translation.x * scale.x,
                    tr.translation.y * scale.y,
                    tr.translation.z * scale.z
                ), tr.rotation, tr.scale)).ToList();
        }

        public static List<TrTransform> TransformPath(List<TrTransform> path, TrTransform tr)
        {
            return path.Select(x => x.TransformBy(tr)).ToList();
        }

        public static Quaternion VectorToRotation(Vector3 vec)
        {
            return Quaternion.LookRotation(vec, Vector3.up);
        }

        public static Vector3 RotationToVector(Quaternion rot)
        {
            return rot * Vector3.forward;
        }

        public static void JitterColor()
        {
            App.BrushColor.CurrentColor = PointerManager.m_Instance.GenerateJitteredColor(
                PointerManager.m_Instance.MainPointer.CurrentBrush.m_ColorLuminanceMin
            );
        }

        public static Vector3 QuadBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, float t)
        {
            float m = 1 - t;
            float a = m * m;
            float b = 2 * m * t;
            float c = t * t;
            float x = a * p0.x + b * p1.x + c * p2.x;
            float y = a * p0.y + b * p1.y + c * p2.y;
            float z = a * p0.z + b * p1.z + c * p2.z;
            return new Vector3(x, y, z);
        }

        public static List<Vector3> QuadBezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, float res)
        {
            var points = new List<Vector3>();
            points.Add(p0);
            for (var t = res; t < 1; t += res)
            {
                points.Add(QuadBezierPoint(p0, p1, p2, t));
            }
            points.Add(p2);
            return points;
        }


    }
}