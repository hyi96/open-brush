// Copyright 2024 The Tilt Brush Authors
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

using TiltBrush;
using TMPro;

public class InspectorInfoTab : InspectorBaseTab
{
    public TextMeshPro m_SummaryText;

    private InspectorPanel m_InspectorPanel;

    void Start()
    {
        m_InspectorPanel = GetComponentInParent<InspectorPanel>();
    }

    public override void OnSelectionChanged()
    {
        m_SummaryText.text = "";

        switch (m_InspectorPanel.CurrentSelectionType)
        {
            case SelectionType.Nothing:
                m_SummaryText.text = "Nothing selected";
                break;
            case SelectionType.Stroke:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = "1 stroke selected";
                }
                else
                {
                    m_SummaryText.text = "{m_InspectorPanel.CurrentSelectionCount} strokes selected";
                }
                break;
            case SelectionType.Image:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = "1 image selected";
                }
                else
                {
                    m_SummaryText.text = "{m_InspectorPanel.CurrentSelectionCount} images selected";
                }
                break;
            case SelectionType.Video:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = "1 video selected";
                }
                else
                {
                    m_SummaryText.text = "{m_InspectorPanel.CurrentSelectionCount} videos selected";
                }
                break;
            case SelectionType.Model:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = "1 model selected";
                }
                else
                {
                    m_SummaryText.text = "{m_InspectorPanel.CurrentSelectionCount} models selected";
                }
                break;
            case SelectionType.Guide:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = "1 guide selected";
                }
                else
                {
                    m_SummaryText.text = "{m_InspectorPanel.CurrentSelectionCount} guides selected";
                }
                break;
            case SelectionType.Mixed:
                m_SummaryText.text = "{m_InspectorPanel.CurrentSelectionCount} items selected";
                break;
        }
    }

}
