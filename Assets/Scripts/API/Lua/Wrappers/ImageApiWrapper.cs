﻿using System;
using System.IO;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A reference image widget")]
    [MoonSharpUserData]
    public class ImageApiWrapper
    {
        public ImageWidget _ImageWidget;

        public ImageApiWrapper(ImageWidget widget)
        {
            _ImageWidget = widget;
        }

        [LuaDocsDescription("The index of the active widget")]
        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_ImageWidget);

        [LuaDocsDescription("Returns a string representation of the image widget")]
        [LuaDocsReturnValue("The string representation of the image widget")]
        public override string ToString()
        {
            return $"Image({_ImageWidget})";
        }

        [LuaDocsDescription("Gets or sets the transform of the image widget")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_ImageWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_ImageWidget.transform] = value;
            }
        }

        [LuaDocsDescription("The 3D position of the Image Widget")]
        public Vector3 position
        {
            get => transform.translation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.T(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.translation = newTransform.translation;
                transform = tr_CS;
            }
        }

        [LuaDocsDescription("Extrudes the image widget with the specified depth and color")]
        [LuaDocsExample(@"Image:Extrude(5, Color.green)")]
        [LuaDocsParameter("depth", "The depth of the extrusion")]
        [LuaDocsParameter("color", "The color of the extrusion")]
        public void Extrude(float depth, ColorApiWrapper color = null)
        {
            var extruder = _ImageWidget.GetComponent<SpriteExtruder>();
            if (depth <= 0)
            {
                extruder.Clear();
            }
            else
            {
                color ??= new ColorApiWrapper(Color.gray);
                extruder.extrudeColor = color._Color;
                extruder.backDistance = depth;
                extruder.Generate();
            }
        }

        [LuaDocsDescription("The 3D orientation of the Image Widget")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.R(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.rotation = newTransform.rotation;
                transform = tr_CS;
            }
        }

        [LuaDocsDescription("The scale of the image widget")]
        public float scale
        {
            get => transform.scale;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.S(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.scale = newTransform.scale;
                transform = tr_CS;
            }
        }

        [LuaDocsDescription("Imports an image widget based on the specified location")]
        [LuaDocsExample(@"Image:Import(""test.png"")")]
        [LuaDocsParameter("location", "The location of the image")]
        [LuaDocsReturnValue("The imported image widget")]
        public static ImageApiWrapper Import(string location) => new (ApiMethods.ImportImage(location));

        [LuaDocsDescription("Selects the image widget")]
        [LuaDocsExample(@"myImage:Select()")]
        public void Select() => ApiMethods.SelectWidget(_ImageWidget);

        [LuaDocsDescription("Deletes the image widget")]
        [LuaDocsExample(@"myImage:Delete()")]
        public void Delete() => ApiMethods.DeleteWidget(_ImageWidget);

        [LuaDocsDescription("Encodes the image as a form")]
        [LuaDocsExample(@"formdata = myImage:FormEncode()")]
        [LuaDocsReturnValue("The encoded image so it can be submitted as a response to a HTML form")]
        public string FormEncode() => Convert.ToBase64String(File.ReadAllBytes(_ImageWidget.ReferenceImage.FileFullPath));

        [LuaDocsDescription("Saves an image as a png based on base64 data")]
        [LuaDocsExample(@"Image:SaveBase64(someData, ""image.png"")")]
        [LuaDocsParameter("base64", "The base64 data for the image")]
        [LuaDocsParameter("filename", "The filename to save as")]
        public string SaveBase64(string base64, string filename) => ApiMethods.SaveBase64(base64, filename);
    }
}