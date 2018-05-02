﻿using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;
using UnityEditor.ProBuilder.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.ProBuilder
{
	/// <summary>
	/// Smoothing groups editor window.
	/// </summary>
	class SmoothGroupEditor : EditorWindow
	{
		class SmoothGroupData
		{
			public bool isVisible;
			public Dictionary<int, List<Face>> groups;
			public Dictionary<int, Color> groupColors;
			public HashSet<int> selected;
			public Mesh previewMesh;
			public Mesh normalsMesh;

			public SmoothGroupData(ProBuilderMesh pb)
			{
				groups = new Dictionary<int, List<Face>>();
				selected = new HashSet<int>();
				groupColors = new Dictionary<int, Color>();
				isVisible = true;

				previewMesh = new Mesh()
				{
					hideFlags = HideFlags.HideAndDontSave,
					name = pb.name + "_SmoothingPreview"
				};

				normalsMesh = new Mesh()
				{
					hideFlags = HideFlags.HideAndDontSave,
					name = pb.name + "_SmoothingNormals"
				};

				Rebuild(pb);
			}

			~SmoothGroupData()
			{
				EditorApplication.delayCall += () =>
				{
					Object.DestroyImmediate(previewMesh);
					Object.DestroyImmediate(normalsMesh);
				};
			}

			public void Rebuild(ProBuilderMesh pb)
			{
				CacheGroups(pb);
				CacheSelected(pb);
				RebuildPreviewMesh(pb);
				RebuildNormalsMesh(pb);
			}

			public void CacheGroups(ProBuilderMesh pb)
			{
				groups.Clear();

				foreach (Face face in pb.facesInternal)
				{
					List<Face> affected;

					if (!groups.TryGetValue(face.smoothingGroup, out affected))
						groups.Add(face.smoothingGroup, new List<Face>() {face});
					else
						affected.Add(face);
				}
			}

			public void CacheSelected(ProBuilderMesh pb)
			{
				selected.Clear();

				foreach(Face face in pb.selectedFaces)
					selected.Add(face.smoothingGroup);
			}

			public void RebuildPreviewMesh(ProBuilderMesh pb)
			{
				List<int> indices = new List<int>();
				Color32[] colors = new Color32[pb.vertexCount];
				groupColors.Clear();

				foreach (KeyValuePair<int, List<Face>> smoothGroup in groups)
				{
					if (smoothGroup.Key > Smoothing.smoothingGroupNone)
					{
						Color32 color = GetDistinctColor(smoothGroup.Key);
						groupColors.Add(smoothGroup.Key, color);
						var groupIndices = smoothGroup.Value.SelectMany(y => y.indices);
						indices.AddRange(groupIndices);
						foreach (int i in groupIndices)
							colors[i] = color;
					}
				}

				previewMesh.Clear();
				previewMesh.vertices = pb.positionsInternal;
				previewMesh.colors32 = colors;
				previewMesh.triangles = indices.ToArray();
			}

			public void RebuildNormalsMesh(ProBuilderMesh pb)
			{
				normalsMesh.Clear();
				Vector3[] srcPositions = pb.mesh.vertices;
				Vector3[] srcNormals = pb.mesh.normals;
				int vertexCount = System.Math.Min(ushort.MaxValue / 2, pb.mesh.vertexCount);
				Vector3[] positions = new Vector3[vertexCount * 2];
				Vector4[] tangents = new Vector4[vertexCount * 2];
				int[] indices = new int[vertexCount * 2];
				for (int i = 0; i < vertexCount; i++)
				{
					int a = i*2, b = i*2+1;

					positions[a] = srcPositions[i];
					positions[b] = srcPositions[i];
					tangents[a] = new Vector4(srcNormals[i].x, srcNormals[i].y, srcNormals[i].z, 0f);
					tangents[b] = new Vector4(srcNormals[i].x, srcNormals[i].y, srcNormals[i].z, 1f);
					indices[a] = a;
					indices[b] = b;
				}
				normalsMesh.vertices = positions;
				normalsMesh.tangents = tangents;
				normalsMesh.subMeshCount = 1;
				normalsMesh.SetIndices(indices, MeshTopology.Lines, 0);
			}
		}

		private static Material m_FaceMaterial = null;
		private static Material smoothPreviewMaterial
		{
			get
			{
				if (m_FaceMaterial == null)
				{
					m_FaceMaterial = new Material(Shader.Find("Hidden/ProBuilder/SmoothingPreview"));
					m_FaceMaterial.hideFlags = HideFlags.HideAndDontSave;
				}

				return m_FaceMaterial;
			}
		}

		private static Material m_NormalPreviewMaterial = null;
		private static Material normalPreviewMaterial
		{
			get
			{
				if (m_NormalPreviewMaterial == null)
					m_NormalPreviewMaterial = new Material(Shader.Find("Hidden/ProBuilder/NormalPreview"));
				return m_NormalPreviewMaterial;
			}
		}

		private static GUIStyle m_GroupButtonStyle = null;
		private static GUIStyle m_GroupButtonSelectedStyle = null;
		private static GUIStyle m_GroupButtonInUseStyle = null;
		private static GUIStyle m_GroupButtonMixedSelectionStyle = null;
		private static GUIStyle m_ColorKeyStyle = null;
		private static GUIStyle m_WordWrappedRichText = null;

		private static GUIStyle groupButtonStyle
		{
			get
			{
				if (m_GroupButtonStyle == null)
				{
					m_GroupButtonStyle = new GUIStyle(GUI.skin.GetStyle("Button"));
					m_GroupButtonStyle.normal.background = IconUtility.GetIcon("Toolbar/Background/RoundedRect_Normal");
					m_GroupButtonStyle.hover.background = IconUtility.GetIcon("Toolbar/Background/RoundedRect_Hover");
					m_GroupButtonStyle.active.background = IconUtility.GetIcon("Toolbar/Background/RoundedRect_Pressed");
					Font asap = FileUtil.LoadInternalAsset<Font>("About/Font/Asap-Regular.otf");
					if (asap != null)
					{
						m_GroupButtonStyle.font = asap;
						m_GroupButtonStyle.fontSize = 12;
						m_GroupButtonStyle.padding = new RectOffset(2, 2, 2, 2);
					}
					m_GroupButtonStyle.border = new RectOffset(3, 3, 3, 3);
					m_GroupButtonStyle.margin = new RectOffset(4, 4, 4, 6);
					m_GroupButtonStyle.alignment = TextAnchor.MiddleCenter;
					m_GroupButtonStyle.fixedWidth = IconWidth;
					m_GroupButtonStyle.fixedHeight = IconHeight;

					// todo Move text & background colors to a global settings file
					m_GroupButtonStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.9f, .9f, .9f) : new Color(.3f, .3f, .3f);
					m_GroupButtonStyle.hover.textColor = EditorGUIUtility.isProSkin ? new Color(.9f, .9f, .9f) : new Color(.3f, .3f, .3f);
					m_GroupButtonStyle.active.textColor = EditorGUIUtility.isProSkin ? new Color(.9f, .9f, .9f) : new Color(.3f, .3f, .3f);
				}
				return m_GroupButtonStyle;
			}
		}

		private static GUIStyle groupButtonSelectedStyle
		{
			get
			{
				if (m_GroupButtonSelectedStyle == null)
				{
					m_GroupButtonSelectedStyle = new GUIStyle(groupButtonStyle);
					m_GroupButtonSelectedStyle.normal.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Normal_Blue");
					m_GroupButtonSelectedStyle.hover.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Hover_Blue");
					m_GroupButtonSelectedStyle.active.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Pressed_Blue");
				}

				return m_GroupButtonSelectedStyle;
			}
		}

		private static GUIStyle groupButtonInUseStyle
		{
			get
			{
				if (m_GroupButtonInUseStyle == null)
				{
					m_GroupButtonInUseStyle = new GUIStyle(groupButtonStyle);
					m_GroupButtonInUseStyle.normal.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Normal_BlueSteel");
					m_GroupButtonInUseStyle.hover.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Hover_BlueSteel");
					m_GroupButtonInUseStyle.active.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Pressed_BlueSteel");
				}

				return m_GroupButtonInUseStyle;
			}
		}

		private static GUIStyle groupButtonMixedSelectionStyle
		{
			get
			{
				if (m_GroupButtonMixedSelectionStyle == null)
				{
					m_GroupButtonMixedSelectionStyle = new GUIStyle(groupButtonStyle);
					m_GroupButtonMixedSelectionStyle.normal.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Normal_Orange");
					m_GroupButtonMixedSelectionStyle.hover.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Hover_Orange");
					m_GroupButtonMixedSelectionStyle.active.background =
						IconUtility.GetIcon("Toolbar/Background/RoundedRect_Pressed_Orange");
				}

				return m_GroupButtonMixedSelectionStyle;
			}
		}

		private static GUIStyle colorKeyStyle
		{
			get
			{
				if (m_ColorKeyStyle == null)
				{
					m_ColorKeyStyle = new GUIStyle(groupButtonStyle);
					m_ColorKeyStyle.fixedWidth = IconWidth;
					m_ColorKeyStyle.fixedHeight = 3;
					m_ColorKeyStyle.padding = new RectOffset(4, 4, 0, 4);
					m_ColorKeyStyle.normal.background = EditorGUIUtility.whiteTexture;
				}
				return m_ColorKeyStyle;
			}
		}

		private static GUIStyle wordWrappedRichText
		{
			get
			{
				if (m_WordWrappedRichText == null)
				{
					m_WordWrappedRichText = new GUIStyle(EditorStyles.wordWrappedLabel);
					m_WordWrappedRichText.richText = true;
					m_WordWrappedRichText.alignment = TextAnchor.LowerLeft;
				}
				return m_WordWrappedRichText;
			}
		}

		private const int IconWidth = 24;
		private const int IconHeight = 24;

		private GUIContent m_GroupKeyContent =
			new GUIContent("21", "Smoothing Group.\n\nRight Click to Select all faces matching this group.");
		private Vector2 m_Scroll = Vector2.zero;
		private GUIContent m_HelpIcon = null;
		private GUIContent m_BreakSmoothingContent = null;
		private GUIContent m_SelectFacesWithSmoothGroupSelectionContent = null;
		private Dictionary<ProBuilderMesh, SmoothGroupData> m_SmoothGroups = new Dictionary<ProBuilderMesh, SmoothGroupData>();
		private static bool m_ShowPreview = false;
		private static bool m_ShowNormals = false;
		private static bool m_IsMovingVertices = false;
		private static bool m_ShowHelp = false;
		private static float m_NormalsSize = 0.1f;
		private static float m_PreviewOpacity = .5f;
		private static bool m_PreviewDither = false;
		private static bool m_ShowSettings = false;

		public static void MenuOpenSmoothGroupEditor()
		{
			bool isUtility = PreferencesInternal.GetBool("pb_SmoothGroupEditor::m_IsWindowUtility", true);
			GetWindow<SmoothGroupEditor>(isUtility, "Smooth Group Editor", true);
		}

		private void OnEnable()
		{
			if (ProBuilderEditor.instance)
			{
				ProBuilderEditor.instance.SetEditLevel(EditLevel.Geometry);
				ProBuilderEditor.instance.SetSelectionMode(SelectMode.Face);
			}

			SceneView.onSceneGUIDelegate += OnSceneGUI;
			Selection.selectionChanged += OnSelectionChanged;
			Undo.undoRedoPerformed += OnSelectionChanged;
			ProBuilderMesh.onElementSelectionChanged += OnElementSelectionChanged;
			ProBuilderEditor.OnVertexMovementBegin += OnBeginVertexMovement;
			ProBuilderEditor.OnVertexMovementFinish += OnFinishVertexMovement;
			this.autoRepaintOnSceneChange = true;
			m_HelpIcon = new GUIContent(IconUtility.GetIcon("Toolbar/Help"), "Open Documentation");
			m_ShowPreview = PreferencesInternal.GetBool("pb_SmoothingGroupEditor::m_ShowPreview", false);
			m_ShowNormals = PreferencesInternal.GetBool("pb_SmoothingGroupEditor::m_DrawNormals", false);
			m_NormalsSize = PreferencesInternal.GetFloat("pb_SmoothingGroupEditor::m_NormalsSize", .1f);
			m_PreviewOpacity = PreferencesInternal.GetFloat("pb_SmoothingGroupEditor::m_PreviewOpacity", .5f);
			m_PreviewDither = PreferencesInternal.GetBool("pb_SmoothingGroupEditor::m_PreviewDither", false);
			m_BreakSmoothingContent = new GUIContent(IconUtility.GetIcon("Toolbar/Face_BreakSmoothing"),
				"Clear the selected faces of their smoothing groups");
			m_SelectFacesWithSmoothGroupSelectionContent = new GUIContent(IconUtility.GetIcon("Toolbar/Selection_SelectBySmoothingGroup"),
				"Expand the face selection by selecting all faces matching the currently selected face groups");
			MeshSelection.OnSelectionChanged();
			OnSelectionChanged();
		}

		private void OnDisable()
		{
			SceneView.onSceneGUIDelegate -= OnSceneGUI;
			Selection.selectionChanged -= OnSelectionChanged;
			Undo.undoRedoPerformed -= OnSelectionChanged;
			ProBuilderMesh.onElementSelectionChanged -= OnElementSelectionChanged;
			m_SmoothGroups.Clear();
		}

		private void OnDestroy()
		{
			m_SmoothGroups.Clear();
		}

		private void OnBeginVertexMovement(ProBuilderMesh[] selection)
		{
			m_IsMovingVertices = true;
		}

		private void OnFinishVertexMovement(ProBuilderMesh[] selection)
		{
			m_IsMovingVertices = false;
			OnSelectionChanged();
		}

		private void OnSelectionChanged()
		{
			m_SmoothGroups.Clear();

			foreach (ProBuilderMesh pb in MeshSelection.Top())
				m_SmoothGroups.Add(pb, new SmoothGroupData(pb));

			this.Repaint();
		}

		private void OnElementSelectionChanged(ProBuilderMesh pb)
		{
			SmoothGroupData data;

			if(!m_SmoothGroups.TryGetValue(pb, out data))
				m_SmoothGroups.Add(pb, data = new SmoothGroupData(pb));
			else
				data.CacheSelected(pb);
		}

		private static void SetWindowIsUtility(bool isUtility)
		{
			PreferencesInternal.SetBool("pb_SmoothGroupEditor::m_IsWindowUtility", isUtility);
			GetWindow<SmoothGroupEditor>().Close();
			MenuOpenSmoothGroupEditor();
		}

		private void OnGUI()
		{
			Event evt = Event.current;

			if (evt.type == EventType.ContextClick)
			{
				bool isUtility = PreferencesInternal.GetBool("pb_SmoothGroupEditor::m_IsWindowUtility", true);

				GenericMenu menu = new GenericMenu();
				menu.AddItem (new GUIContent("Open As Floating Window", ""), isUtility, () => SetWindowIsUtility(true));
				menu.AddItem (new GUIContent("Open As Dockable Window", ""), !isUtility, () => SetWindowIsUtility(false));
				menu.ShowAsContext ();
			}

			GUILayout.BeginHorizontal(EditorStyles.toolbar);

			if (GUILayout.Button("Settings",
				m_ShowSettings ? UI.EditorGUIUtility.GetOnStyle(EditorStyles.toolbarButton) : EditorStyles.toolbarButton))
				m_ShowSettings = !m_ShowSettings;

			if (GUILayout.Button("Preview",
				m_ShowPreview ? UI.EditorGUIUtility.GetOnStyle(EditorStyles.toolbarButton) : EditorStyles.toolbarButton))
			{
				m_ShowPreview = !m_ShowPreview;
				PreferencesInternal.SetBool("pb_SmoothingGroupEditor::m_ShowPreview", m_ShowPreview);
			}

			if (GUILayout.Button("Normals",
				m_ShowNormals ? UI.EditorGUIUtility.GetOnStyle(EditorStyles.toolbarButton) : EditorStyles.toolbarButton))
			{
				m_ShowNormals = !m_ShowNormals;
				PreferencesInternal.SetBool("pb_SmoothingGroupEditor::m_DrawNormals", m_ShowNormals);
			}

			if (m_ShowNormals)
			{
				EditorGUI.BeginChangeCheck();

				m_NormalsSize = GUILayout.HorizontalSlider(
					m_NormalsSize,
					.001f,
					1f,
					GUILayout.MinWidth(30f),
					GUILayout.MaxWidth(100f));

				if (EditorGUI.EndChangeCheck())
				{
					PreferencesInternal.SetFloat("pb_SmoothingGroupEditor::m_NormalsSize", m_NormalsSize);
					foreach (var kvp in m_SmoothGroups)
						kvp.Value.RebuildNormalsMesh(kvp.Key);
					SceneView.RepaintAll();
				}
			}

			GUILayout.FlexibleSpace();

			if(GUILayout.Button(m_HelpIcon, UI.EditorStyles.toolbarHelpIcon))
				m_ShowHelp = !m_ShowHelp;
			GUILayout.EndHorizontal();

			if (m_ShowSettings)
			{
				GUILayout.BeginVertical(UI.EditorStyles.settingsGroup);

				EditorGUIUtility.labelWidth = 100;

				EditorGUI.BeginChangeCheck();

				m_PreviewOpacity = EditorGUILayout.Slider("Preview Opacity", m_PreviewOpacity, .001f, 1f);
				m_PreviewDither = EditorGUILayout.Toggle("Preview Dither", m_PreviewDither);

				if (EditorGUI.EndChangeCheck())
				{
					PreferencesInternal.SetFloat("pb_SmoothingGroupEditor::m_PreviewOpacity", m_PreviewOpacity);
					PreferencesInternal.SetBool("pb_SmoothingGroupEditor::m_PreviewDither", m_PreviewDither);
					smoothPreviewMaterial.SetFloat("_Opacity", m_PreviewOpacity);
					smoothPreviewMaterial.SetFloat("_Dither", m_PreviewDither ? 1f : 0f);
					SceneView.RepaintAll();
				}

				EditorGUIUtility.labelWidth = 0;

				GUILayout.EndVertical();
			}

			m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

			if (m_ShowHelp)
			{
				GUILayout.BeginVertical(UI.EditorStyles.settingsGroup);

				GUILayout.Label("Create and Clear Smoothing Groups", EditorStyles.boldLabel);

				GUILayout.Label("Adjacent faces with the same smoothing group will appear to have a soft adjoining edge.", wordWrappedRichText);
				GUILayout.Space(2);
				GUILayout.Label("<b>To smooth</b> a selected group of faces, click one of the Smooth Group buttons.", wordWrappedRichText);
				GUILayout.Label("<b>To clear</b> selected faces of their smooth group, click the [Break] icon.", wordWrappedRichText);
				GUILayout.Label("<b>To select</b> all faces in a group, Right+Click or Alt+Click a smooth group button.", wordWrappedRichText);
				GUILayout.Space(2);

				global::UnityEditor.ProBuilder.UI.EditorGUILayout.BeginRow();
				GUILayout.Button("1", groupButtonStyle);
				GUILayout.Label("An unused smooth group", wordWrappedRichText);
				global::UnityEditor.ProBuilder.UI.EditorGUILayout.EndRow();

				global::UnityEditor.ProBuilder.UI.EditorGUILayout.BeginRow();
				GUILayout.Button("1", groupButtonInUseStyle);
				GUILayout.Label("A smooth group that is in use, but not in the current selection", wordWrappedRichText);
				global::UnityEditor.ProBuilder.UI.EditorGUILayout.EndRow();

				global::UnityEditor.ProBuilder.UI.EditorGUILayout.BeginRow();
				GUILayout.Button("1", groupButtonSelectedStyle);
				GUILayout.Label("A smooth group that is currently selected", wordWrappedRichText);
				global::UnityEditor.ProBuilder.UI.EditorGUILayout.EndRow();

				global::UnityEditor.ProBuilder.UI.EditorGUILayout.BeginRow();
				GUILayout.Button("1", groupButtonMixedSelectionStyle);
				GUI.backgroundColor = Color.white;
				GUILayout.Label("A smooth group is selected, but the selection also contains non-grouped faces", wordWrappedRichText);
				global::UnityEditor.ProBuilder.UI.EditorGUILayout.EndRow();

				if(GUILayout.Button("Open Documentation"))
					Application.OpenURL("http://procore3d.github.io/probuilder2/toolbar/tool-panels/#smoothing-groups");

				GUILayout.EndVertical();
			}

			// border style is 4 margin, 4 pad, 1px content. inner is accounted for by btn size + btn margin.
			float area = (position.width - 10);
			float margin = Mathf.Max(groupButtonStyle.margin.left, groupButtonStyle.margin.right);
			int columns = (int)(area / (groupButtonStyle.CalcSize(m_GroupKeyContent).x + margin)) - 1;

			if (m_SmoothGroups.Count < 1)
			{
				GUILayout.BeginVertical();
				GUILayout.FlexibleSpace();
				GUILayout.Label("Select a ProBuilder Mesh", UI.EditorGUIUtility.CenteredGreyMiniLabel);
				GUILayout.FlexibleSpace();
				GUILayout.EndVertical();
			}
			else
			{
				foreach (var mesh in m_SmoothGroups)
				{
					ProBuilderMesh pb = mesh.Key;
					SmoothGroupData data = mesh.Value;

					GUILayout.BeginVertical(UI.EditorStyles.settingsGroup);

					GUILayout.BeginHorizontal();

					if(GUILayout.Button(pb.name, UI.EditorStyles.headerLabel))
						data.isVisible = !data.isVisible;

					GUILayout.FlexibleSpace();

					if (GUILayout.Button(m_SelectFacesWithSmoothGroupSelectionContent,
						UI.EditorStyles.buttonStyle))
						SelectGroups(pb, new HashSet<int>(pb.selectedFaces.Select(x => x.smoothingGroup)));

					if (GUILayout.Button(m_BreakSmoothingContent,
						UI.EditorStyles.buttonStyle))
						SetGroup(pb, Smoothing.smoothingGroupNone);

					GUILayout.EndHorizontal();

					bool isMixedSelection = data.selected.Contains(Smoothing.smoothingGroupNone);

					if (data.isVisible)
					{
						int column = 0;
						bool anySmoothGroups = data.groups.Any(x => x.Key > Smoothing.smoothingGroupNone);

						GUILayout.BeginHorizontal();

						for (int i = 1; i < Smoothing.smoothRangeMax; i++)
						{
							bool isSelected = data.selected.Contains(i);

							GUIStyle stateStyle = isSelected ?
								(isMixedSelection ? groupButtonMixedSelectionStyle : groupButtonSelectedStyle) :
								data.groups.ContainsKey(i) ? groupButtonInUseStyle : groupButtonStyle;

							if (m_ShowPreview && anySmoothGroups)
								GUILayout.BeginVertical(GUILayout.MaxWidth(IconWidth));

							m_GroupKeyContent.text = i.ToString();

							if (GUILayout.Button(m_GroupKeyContent, stateStyle))
							{
								// if right click or alt click select the faces instead of setting a group
								if((Event.current.modifiers & EventModifiers.Alt) == EventModifiers.Alt ||
								   Event.current.button != 0)
									SelectGroups(pb, new HashSet<int>() { i });
								else
									SetGroup(pb, i);
							}

							if (m_ShowPreview && anySmoothGroups)
							{
								GUI.backgroundColor = data.groupColors.ContainsKey(i) ? data.groupColors[i] : Color.clear;
								GUILayout.Label("", colorKeyStyle);
								GUILayout.EndVertical();
								GUI.backgroundColor = Color.white;
							}

							if (++column > columns)
							{
								column = 0;
								GUILayout.EndHorizontal();
								GUILayout.BeginHorizontal();
							}
						}

						GUILayout.EndHorizontal();
					}

					GUILayout.EndVertical();
				}
			}

			EditorGUILayout.EndScrollView();

			// This isn't great, but we need hover previews to work
			if(mouseOverWindow == this)
				Repaint();
		}

		void OnSceneGUI(SceneView view)
		{
			if (m_SmoothGroups.Count > 1)
			{
				using (new HandleGUI())
				{
					foreach (var kvp in m_SmoothGroups)
						Handles.Label(kvp.Key.transform.position, kvp.Key.name, EditorStyles.boldLabel);
				}
			}

			Event evt = Event.current;

			if (!m_IsMovingVertices && evt.type == EventType.Repaint)
			{

				foreach (var kvp in m_SmoothGroups)
				{
					if (m_ShowPreview)
					{
						Mesh m = kvp.Value.previewMesh;

						if (m != null)
						{
							smoothPreviewMaterial.SetPass(0);
							Graphics.DrawMeshNow(m, kvp.Key.transform.localToWorldMatrix);
						}
					}

					if (m_ShowNormals)
					{
						Mesh m = kvp.Value.normalsMesh;

						if (m != null)
						{
							Transform trs = kvp.Key.transform;
							normalPreviewMaterial.SetFloat("_Scale", m_NormalsSize * HandleUtility.GetHandleSize(trs.GetComponent<MeshRenderer>().bounds.center));
							normalPreviewMaterial.SetPass(0);
							Graphics.DrawMeshNow(m, trs.localToWorldMatrix);
						}
					}
				}
			}
		}

		private static void SelectGroups(ProBuilderMesh pb, HashSet<int> groups)
		{
			UndoUtility.RecordSelection(pb, "Select with Smoothing Group");

			if( (Event.current.modifiers & EventModifiers.Shift) == EventModifiers.Shift ||
				(Event.current.modifiers & EventModifiers.Control) == EventModifiers.Control )
				pb.SetSelectedFaces(pb.facesInternal.Where(x => groups.Contains(x.smoothingGroup) || pb.selectedFaces.Contains(x)));
			else
				pb.SetSelectedFaces(pb.facesInternal.Where(x => groups.Contains(x.smoothingGroup)));
			ProBuilderEditor.Refresh();
		}

		private void SetGroup(ProBuilderMesh pb, int index)
		{
			UndoUtility.RecordObject(pb, "Set Smoothing Group");

			foreach (Face face in pb.selectedFaceCount < 1 ? pb.facesInternal : pb.selectedFaces)
				face.smoothingGroup = index;

			// todo pb.Rebuild
			pb.ToMesh();
			pb.Refresh();
			pb.Optimize();

			SmoothGroupData data;

			if(!m_SmoothGroups.TryGetValue(pb, out data))
				m_SmoothGroups.Add(pb, new SmoothGroupData(pb));
			else
				data.Rebuild(pb);

			ProBuilderEditor.Refresh();
		}

		private static Color32 GetDistinctColor(int index)
		{
			return m_KellysMaxContrastSet[index % m_KellysMaxContrastSet.Length];
		}

		private static readonly Color32[] m_KellysMaxContrastSet = new Color32[]
		{
			new Color32(230, 25, 75, 255), 		// Red
			new Color32(60, 180, 75, 255), 		// Green
			new Color32(255, 225, 25, 255), 	// Yellow
			new Color32(0, 130, 200, 255), 		// Blue
			new Color32(245, 130, 48, 255), 	// Orange
			new Color32(145, 30, 180, 255), 	// Purple
			new Color32(70, 240, 240, 255), 	// Cyan
			new Color32(240, 50, 230, 255), 	// Magenta
			new Color32(210, 245, 60, 255), 	// Lime
			new Color32(250, 190, 190, 255), 	// Pink
			new Color32(0, 128, 128, 255), 		// Teal
			new Color32(230, 190, 255, 255), 	// Lavender
			new Color32(170, 110, 40, 255), 	// Brown
			new Color32(255, 250, 200, 255), 	// Beige
			new Color32(128, 0, 0, 255), 		// Maroon
			new Color32(170, 255, 195, 255), 	// Mint
			new Color32(128, 128, 0, 255), 		// Olive
			new Color32(255, 215, 180, 255), 	// Coral
			new Color32(0, 0, 128, 255), 		// Navy
			new Color32(128, 128, 128, 255), 	// Grey
			new Color32(255, 255, 255, 255), 	// White
			new Color32(0, 0, 0, 255), 			// Black
		};
	}
}
