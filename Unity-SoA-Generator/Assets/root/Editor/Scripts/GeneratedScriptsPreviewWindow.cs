using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Saesentsessis.DOD.SoA.CodeGen;
using UnityEngine;
using UnityEditor;

namespace Saesentsessis.DOD.SoA.Editor
{
    public class GeneratedScriptsPreviewWindow : EditorWindow
    {
        private const int DefaultSampleCapacity = 1024;

        // Colors.
        private static readonly Color BadgeEnabledColor = new Color(0.55f, 0.90f, 0.55f);
        private static readonly Color BadgeDisabledColor = new Color(0.60f, 0.60f, 0.60f);
        private static readonly Color NativeExistsColor = new Color(0.40f, 0.85f, 0.40f);
        private static readonly Color NativeMissingColor = new Color(0.90f, 0.40f, 0.40f);

        // Bullseye glyph reads as "ping / locate" for the header Select button.
        private static readonly GUIContent SelectButtonContent = new GUIContent("\U0001F4C1", "Ping the script declaring this struct");

        // Layout metrics.
        private const float LeftMargin = 6f;
        private const float InnerPadding = 6f;
        private const float SubIndent = 12f;
        private const float EntrySpacing = 4f;
        private const float BadgeGap = 6f;
        private const float BadgeWidthPadding = 4f;
        private const float ScrollbarWidth = 16f;
        private const float LabelColumnWidth = 110f;
        private const float CapacityFieldWidth = 90f;
        private const float RefreshButtonWidth = 70f;
        private const float SelectButtonWidth = 22f;
        private const float SummaryWidth = 220f;
        private const float HelpBoxHeight = 38f;
        private const float TopPadding = 4f;

        private static float LineHeight => EditorGUIUtility.singleLineHeight;
        private static float Spacing => EditorGUIUtility.standardVerticalSpacing;
        private static float RowStep => LineHeight + Spacing;

        private struct Badge
        {
            public string Text;
            public bool Enabled;
            public float Width;
        }

        /// <summary>
        /// Cached, immutable snapshot of a single tagged struct and its generated containers.
        /// All display strings are precomputed during <see cref="RebuildEntries"/> so the repaint
        /// loop performs no string interpolation, boxing, or reflection.
        /// </summary>
        private sealed class StructEntry
        {
            public string StructName;
            public string StructKey;
            public Type StructType;
            public Badge[] Badges;

            public bool UnsafeFound;
            public string UnsafeFullName;
            public string ElementSizeText;
            public string FlagCountText;
            public string ByteSizeLabel;
            public string ByteSizeText;
            public string PaddingWasteText;
            public string BitPackWasteText;
            public string UnsafeMissingText;

            public bool NativeExists;
            public string NativeStatusText;
        }

        private readonly List<StructEntry> _entries = new List<StructEntry>();
        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, MonoScript> _scriptCache = new Dictionary<string, MonoScript>();
        private readonly GUIContent _measureContent = new GUIContent();

        private int _sampleCapacity = DefaultSampleCapacity;
        private string _summaryText = string.Empty;
        private bool _badgeWidthsDirty;
        private Vector2 _scrollPosition;

        [MenuItem("Window/Saesentsessis/SoA Preview Window")]
        public static void CreateWindow()
        {
            var window = CreateWindow<GeneratedScriptsPreviewWindow>();
            window.titleContent = new GUIContent("SoA Preview");
            window.Show();
        }

        private void OnEnable()
        {
            RebuildEntries();
        }

        private void RebuildEntries()
        {
            _entries.Clear();
            _scriptCache.Clear();

            var taggedTypes = TypeCache.GetTypesWithAttribute<GenerateSoAAttribute>();
            var soaTypes = TypeCache.GetTypesDerivedFrom<IStructureOfArrays>();

            var typesByFullName = new Dictionary<string, Type>(soaTypes.Count);
            foreach (var soaType in soaTypes)
            {
                if (string.IsNullOrEmpty(soaType.FullName) == false)
                    typesByFullName[soaType.FullName] = soaType;
            }

            var sampleCapacity = Mathf.Max(1, _sampleCapacity);

            foreach (var taggedType in taggedTypes)
            {
                var attribute = taggedType.GetCustomAttribute<GenerateSoAAttribute>();
                if (attribute == null)
                    continue;

                var finalName = string.IsNullOrEmpty(attribute.StructName)
                    ? taggedType.Name + "SoA"
                    : attribute.StructName;

                var containerNamespace = string.IsNullOrEmpty(attribute.StructNamespace)
                    ? (taggedType.Namespace ?? string.Empty)
                    : attribute.StructNamespace;

                var unsafeName = "Unsafe" + finalName;
                var nativeName = "Native" + finalName;

                var unsafeFullName = string.IsNullOrEmpty(containerNamespace)
                    ? unsafeName
                    : containerNamespace + "." + unsafeName;
                var nativeFullName = string.IsNullOrEmpty(containerNamespace)
                    ? nativeName
                    : containerNamespace + "." + nativeName;

                var entry = new StructEntry
                {
                    StructName = taggedType.Name,
                    StructKey = taggedType.FullName ?? taggedType.Name,
                    StructType = taggedType,
                    UnsafeFullName = unsafeFullName,
                    UnsafeMissingText = $"UnsafeSoA '{unsafeName}' was not generated.",
                    ByteSizeLabel = $"Byte Size @ {sampleCapacity}",
                    Badges = new[]
                    {
                        new Badge { Text = BadgeText("Flatten", attribute.AllowFieldHierarchyFlattening), Enabled = attribute.AllowFieldHierarchyFlattening },
                        new Badge { Text = BadgeText("BitPack", attribute.AllowBooleanBitPacking), Enabled = attribute.AllowBooleanBitPacking },
                        new Badge { Text = BadgeText("Native", attribute.GenerateNativeContainer), Enabled = attribute.GenerateNativeContainer },
                    },
                };

                if (typesByFullName.TryGetValue(unsafeFullName, out var unsafeType))
                    ReadUnsafeInfo(entry, unsafeType, sampleCapacity);

                entry.NativeExists = typesByFullName.ContainsKey(nativeFullName);
                entry.NativeStatusText = entry.NativeExists ? "Exists: " + nativeName : "Not generated";

                _entries.Add(entry);
            }

            _entries.Sort((a, b) => string.Compare(a.StructName, b.StructName, StringComparison.Ordinal));

            _summaryText = $"Tagged: {taggedTypes.Count}    Generated: {soaTypes.Count}";
            _badgeWidthsDirty = true;
        }

        private static string BadgeText(string label, bool enabled)
        {
            return (enabled ? "\u2713 " : "\u2715 ") + label;
        }

        /// <summary>
        /// Reads the container's core information at the rebuild stage. A default (non-allocating)
        /// instance is created so ElementSize/FlagCount can be read through the
        /// <see cref="IStructureOfArrays"/> interface; ByteSize is derived with the same
        /// <see cref="Bitwise.ComputeByteSize"/> formula the interface getter uses, avoiding any
        /// native allocation for the sample capacity.
        /// </summary>
        private static void ReadUnsafeInfo(StructEntry entry, Type unsafeType, int sampleCapacity)
        {
            try
            {
                var instance = (IStructureOfArrays)Activator.CreateInstance(unsafeType);

                int elementSize = instance.ElementSize;
                int flagCount = instance.FlagCount;
                long byteSize = Bitwise.ComputeByteSize(elementSize, sampleCapacity, flagCount);

                long paddingWaste = (long)ReadPaddingPerElement(unsafeType) * sampleCapacity;

                // The bit-packing block rounds up to whole 64-bit words per flag, and the primitive
                // block is padded up to an 8-byte boundary before it. Both only exist when flags are packed.
                long bitPackWaste = 0;
                if (flagCount > 0)
                {
                    long wastedBits = (long)flagCount * (Bitwise.AlignUp(sampleCapacity, 64) - sampleCapacity);
                    long primitiveBlock = (long)elementSize * sampleCapacity;
                    long alignmentGap = Bitwise.AlignUp(primitiveBlock, 8) - primitiveBlock;
                    bitPackWaste = wastedBits / 8 + alignmentGap;
                }

                entry.ElementSizeText = elementSize + " bytes";
                entry.FlagCountText = flagCount.ToString();
                entry.ByteSizeText = byteSize.ToString("N0") + " bytes";
                entry.PaddingWasteText = paddingWaste.ToString("N0") + " bytes";
                entry.BitPackWasteText = bitPackWaste.ToString("N0") + " bytes";
                entry.UnsafeFound = true;
            }
            catch (Exception exception)
            {
                entry.UnsafeFound = false;
                Debug.LogWarning($"[SoA Preview] Failed to inspect '{unsafeType.FullName}': {exception.Message}");
            }
        }

        /// <summary>
        /// Reads the generator-emitted <c>PaddingPerElement</c> const via reflection. Returns 0 when the
        /// const is absent (e.g. a stale type generated before this metadata was added).
        /// </summary>
        private static int ReadPaddingPerElement(Type unsafeType)
        {
            var field = unsafeType.GetField("PaddingPerElement", BindingFlags.Public | BindingFlags.Static);
            
            if (field != null && field.IsLiteral && field.FieldType == typeof(int))
                return (int)field.GetRawConstantValue();

            return 0;
        }

        private void PingStructScript(StructEntry entry)
        {
            if (entry.StructType == null)
                return;

            if (_scriptCache.TryGetValue(entry.StructKey, out var script) == false)
            {
                script = ResolveScript(entry.StructType);
                _scriptCache[entry.StructKey] = script;
            }

            if (script == null)
            {
                Debug.LogWarning($"[SoA Preview] Could not locate the source script declaring '{entry.StructName}'.");
                return;
            }

            Selection.activeObject = script;
            EditorGUIUtility.PingObject(script);
        }

        /// <summary>
        /// Locates the MonoScript that declares the given struct. Plain structs do not resolve via
        /// MonoScript.GetClass(), so the file text is scanned for the declaration instead.
        /// </summary>
        private static MonoScript ResolveScript(Type type)
        {
            var declarationPattern = new Regex($@"\bstruct\s+{Regex.Escape(type.Name)}\b");

            var namedMatch = MatchScript(AssetDatabase.FindAssets($"t:MonoScript {type.Name}"), declarationPattern);
            if (namedMatch != null)
                return namedMatch;

            return MatchScript(AssetDatabase.FindAssets("t:MonoScript"), declarationPattern);
        }

        private static MonoScript MatchScript(string[] guids, Regex declarationPattern)
        {
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null)
                    continue;

                var text = script.text;
                if (string.IsNullOrEmpty(text) == false && declarationPattern.IsMatch(text))
                    return script;
            }

            return null;
        }

        private void RecalculateBadgeWidths()
        {
            var style = EditorStyles.miniLabel;
            foreach (var entry in _entries)
            {
                var badges = entry.Badges;
                for (int i = 0; i < badges.Length; i++)
                {
                    _measureContent.text = badges[i].Text;
                    badges[i].Width = style.CalcSize(_measureContent).x + BadgeWidthPadding;
                }
            }

            _badgeWidthsDirty = false;
        }

        private void OnGUI()
        {
            if (_badgeWidthsDirty)
                RecalculateBadgeWidths();

            var toolbarHeight = EditorStyles.toolbar.fixedHeight;
            DrawToolbar(toolbarHeight);

            var clipRect = new Rect(0f, toolbarHeight, position.width, position.height - toolbarHeight);

            if (_entries.Count == 0)
            {
                var infoRect = new Rect(clipRect.x + LeftMargin, clipRect.y + TopPadding,
                    clipRect.width - LeftMargin * 2f, HelpBoxHeight);
                EditorGUI.HelpBox(infoRect, "No structs tagged with [GenerateSoA] were found.", MessageType.Info);
                return;
            }

            var contentHeight = TopPadding;
            foreach (var entry in _entries)
                contentHeight += MeasureEntryHeight(entry) + EntrySpacing;

            var needsScrollbar = contentHeight > clipRect.height;
            var viewWidth = needsScrollbar ? clipRect.width - ScrollbarWidth : clipRect.width;
            var viewRect = new Rect(0f, 0f, viewWidth, contentHeight);

            _scrollPosition = GUI.BeginScrollView(clipRect, _scrollPosition, viewRect);

            var y = TopPadding;
            var entryWidth = viewWidth - LeftMargin * 2f;
            foreach (var entry in _entries)
            {
                var height = MeasureEntryHeight(entry);
                var entryRect = new Rect(LeftMargin, y, entryWidth, height);
                DrawEntry(entryRect, entry);
                y += height + EntrySpacing;
            }

            GUI.EndScrollView();
        }

        private void DrawToolbar(float toolbarHeight)
        {
            var toolbarRect = new Rect(0f, 0f, position.width, toolbarHeight);
            GUI.Label(toolbarRect, GUIContent.none, EditorStyles.toolbar);

            var x = LeftMargin;
            var labelRect = new Rect(x, 0f, LabelColumnWidth, toolbarHeight);
            GUI.Label(labelRect, "Sample Capacity");
            x += LabelColumnWidth;

            var fieldRect = new Rect(x, 1f, CapacityFieldWidth, toolbarHeight - 2f);
            EditorGUI.BeginChangeCheck();
            var newCapacity = EditorGUI.IntField(fieldRect, _sampleCapacity, EditorStyles.toolbarTextField);
            if (EditorGUI.EndChangeCheck())
            {
                _sampleCapacity = Mathf.Max(1, newCapacity);
                RebuildEntries();
            }

            x += CapacityFieldWidth + BadgeGap;
            var refreshRect = new Rect(x, 0f, RefreshButtonWidth, toolbarHeight);
            if (GUI.Button(refreshRect, "Refresh", EditorStyles.toolbarButton))
                RebuildEntries();

            var summaryRect = new Rect(position.width - SummaryWidth - LeftMargin, 0f, SummaryWidth, toolbarHeight);
            GUI.Label(summaryRect, _summaryText, EditorStyles.miniLabel);
        }

        private float MeasureEntryHeight(StructEntry entry)
        {
            var height = InnerPadding + RowStep;

            if (IsExpanded(entry.StructKey))
            {
                height += entry.UnsafeFound ? 7f * RowStep : HelpBoxHeight + Spacing;
                height += RowStep;
            }

            return height + InnerPadding;
        }

        private void DrawEntry(Rect entryRect, StructEntry entry)
        {
            GUI.Box(entryRect, GUIContent.none, EditorStyles.helpBox);

            var contentX = entryRect.x + InnerPadding;
            var contentWidth = entryRect.width - InnerPadding * 2f;
            var y = entryRect.y + InnerPadding;

            var expanded = DrawHeader(new Rect(contentX, y, contentWidth, LineHeight), entry);
            y += RowStep;

            if (expanded == false)
                return;

            if (entry.UnsafeFound)
            {
                EditorGUI.LabelField(new Rect(contentX, y, contentWidth, LineHeight), "UnsafeSoA", EditorStyles.boldLabel);
                y += RowStep;

                var subX = contentX + SubIndent;
                var subWidth = contentWidth - SubIndent;
                EditorGUI.LabelField(new Rect(subX, y, subWidth, LineHeight), "Type", entry.UnsafeFullName);
                y += RowStep;
                EditorGUI.LabelField(new Rect(subX, y, subWidth, LineHeight), "Element Size", entry.ElementSizeText);
                y += RowStep;
                EditorGUI.LabelField(new Rect(subX, y, subWidth, LineHeight), "Flag Count", entry.FlagCountText);
                y += RowStep;
                EditorGUI.LabelField(new Rect(subX, y, subWidth, LineHeight), entry.ByteSizeLabel, entry.ByteSizeText);
                y += RowStep;
                EditorGUI.LabelField(new Rect(subX, y, subWidth, LineHeight), "Padding Waste", entry.PaddingWasteText);
                y += RowStep;
                EditorGUI.LabelField(new Rect(subX, y, subWidth, LineHeight), "Bit-Packing Waste", entry.BitPackWasteText);
                y += RowStep;
            }
            else
            {
                EditorGUI.HelpBox(new Rect(contentX, y, contentWidth, HelpBoxHeight), entry.UnsafeMissingText, MessageType.Error);
                y += HelpBoxHeight + Spacing;
            }

            DrawNativeRow(new Rect(contentX, y, contentWidth, LineHeight), entry);
        }

        private bool DrawHeader(Rect rect, StructEntry entry)
        {
            var previousColor = GUI.color;

            // Select button pinned to the rightmost position of the header.
            var selectRect = new Rect(rect.xMax - SelectButtonWidth, rect.y, SelectButtonWidth, rect.height);
            if (GUI.Button(selectRect, SelectButtonContent, EditorStyles.miniButton))
                PingStructScript(entry);

            var badgeX = selectRect.x - BadgeGap;
            var badges = entry.Badges;
            for (int i = badges.Length - 1; i >= 0; i--)
            {
                var badge = badges[i];
                badgeX -= badge.Width;
                GUI.color = badge.Enabled ? BadgeEnabledColor : BadgeDisabledColor;
                GUI.Label(new Rect(badgeX, rect.y, badge.Width, rect.height), badge.Text, EditorStyles.miniLabel);
                badgeX -= BadgeGap;
            }

            GUI.color = previousColor;

            var foldoutRect = new Rect(rect.x, rect.y, Mathf.Max(0f, badgeX - rect.x), rect.height);
            var expanded = EditorGUI.Foldout(foldoutRect, IsExpanded(entry.StructKey), entry.StructName, true);
            _foldoutStates[entry.StructKey] = expanded;
            return expanded;
        }

        private static void DrawNativeRow(Rect rect, StructEntry entry)
        {
            EditorGUI.LabelField(new Rect(rect.x, rect.y, LabelColumnWidth, rect.height), "NativeSoA");

            var statusRect = new Rect(rect.x + LabelColumnWidth, rect.y, rect.width - LabelColumnWidth, rect.height);
            var previousColor = GUI.color;
            GUI.color = entry.NativeExists ? NativeExistsColor : NativeMissingColor;
            GUI.Label(statusRect, entry.NativeStatusText, EditorStyles.boldLabel);
            GUI.color = previousColor;
        }

        private bool IsExpanded(string key)
        {
            _foldoutStates.TryGetValue(key, out var expanded);
            return expanded;
        }
    }
}
