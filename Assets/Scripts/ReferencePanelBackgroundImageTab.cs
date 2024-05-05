﻿// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using UnityEngine;

namespace TiltBrush
{
    public class ReferencePanelBackgroundImageTab : ReferencePanelTab
    {
        public class BackgroundImageIcon : ReferenceIcon
        {
            public ReferencePanel Parent { get; set; }
            public bool TextureAssigned { get; set; }

            public LoadBackgroundImageButton BackgroundImageButton
            {
                get { return Button as LoadBackgroundImageButton; }
            }

            public override void Refresh(int iCatalog)
            {
                Button.SetButtonTexture(Parent.UnknownImageTexture, 1);

                //set sketch index relative to page based index
                var image = BackgroundImageCatalog.m_Instance.IndexToImage(iCatalog);
                BackgroundImageButton.ReferenceImage = image;
                BackgroundImageButton.RefreshDescription();

                //init icon according to availability of sketch
                if (image != null)
                {
                    Button.gameObject.SetActive(true);
                    TextureAssigned = false;
                }
                else
                {
                    Button.gameObject.SetActive(false);
                    TextureAssigned = true;
                }
            }
        }

        [SerializeField] protected bool m_AutoLoadImages;
        private bool m_AllIconTexturesAssigned;
        private bool m_tabAccessed;

        public override IReferenceItemCatalog Catalog
        {
            get { return BackgroundImageCatalog.m_Instance; }
        }
        public override ReferenceButton.Type ReferenceButtonType
        {
            get { return ReferenceButton.Type.BackgroundImages; }
        }
        protected override Type ButtonType
        {
            get { return typeof(LoadBackgroundImageButton); }
        }
        protected override Type IconType
        {
            get { return typeof(BackgroundImageIcon); }
        }

        public override void RefreshTab(bool selected)
        {
            base.RefreshTab(selected);
            if (selected)
            {
                if (m_AutoLoadImages)
                {
                    BackgroundImageCatalog.m_Instance.RequestLoadImages(m_IndexOffset,
                        m_IndexOffset + m_Icons.Length);
                }
                m_AllIconTexturesAssigned = false;
            }
        }

        public override void InitTab()
        {
            base.InitTab();
            foreach (var icon in m_Icons)
            {
                (icon as BackgroundImageIcon).Parent = GetComponentInParent<ReferencePanel>();
            }
        }

        public override void UpdateTab()
        {
            base.UpdateTab();
            if (!m_AllIconTexturesAssigned)
            {
                m_AllIconTexturesAssigned = true;

                //poll sketch catalog until icons have loaded
                for (int i = 0; i < m_Icons.Length; ++i)
                {
                    var imageIcon = m_Icons[i] as BackgroundImageIcon;
                    var btn = imageIcon.Button as LoadBackgroundImageButton;
                    if (!imageIcon.TextureAssigned)
                    {
                        int iMapIndex = m_IndexOffset + i;
                        float aspect;
                        Texture2D rTexture = BackgroundImageCatalog.m_Instance.GetImageIcon(iMapIndex, out aspect);
                        if (rTexture != null)
                        {
                            btn.Set360ButtonTexture(rTexture, aspect);
                            imageIcon.TextureAssigned = true;
                        }
                        else
                        {
                            m_AllIconTexturesAssigned = false;
                        }
                    }
                }
            }
        }

        public override void OnTabEnable()
        {
            base.OnTabEnable();
            if (!m_tabAccessed && m_AutoLoadImages)
            {
                StartCoroutine(OverlayManager.m_Instance.RunInCompositor(
                    OverlayType.LoadImages,
                    BackgroundImageCatalog.m_Instance.LoadAllImagesCoroutine(),
                    fadeDuration: 0.25f));
                m_tabAccessed = true;
            }
        }

        public override void OnUpdateGazeBehavior(Color panelColor, bool gazeActive, bool available)
        {
            base.OnUpdateGazeBehavior(panelColor, gazeActive, available);
            bool? buttonsGrayscale = null;
            if (!gazeActive)
            {
                buttonsGrayscale = true;
            }
            else if (available)
            {
                buttonsGrayscale = false;
            }
            else
            {
                // Don't mess with grayscale-ness
            }

            if (buttonsGrayscale != null)
            {
                foreach (var icon in m_Icons)
                {
                    icon.Button.SetButtonGrayscale(buttonsGrayscale.Value);
                }
            }
        }
    }
} // namespace TiltBrush
