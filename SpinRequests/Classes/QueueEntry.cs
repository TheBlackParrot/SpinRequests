using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpinCore.UI;
using SpinRequests.Services;
using SpinRequests.UI;
using SpinShareLib.Types;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using XDMenuPlay;
using Object = UnityEngine.Object;

namespace SpinRequests.Classes;

public class QueueEntry
{
    [JsonIgnore] private static readonly Dictionary<string, string> DifficultyAbbreviations = new()
    {
        { "Easy", "E" },
        { "Normal", "N" },
        { "Hard", "H" },
        { "Expert", "EX" },
        { "XD", "XD" }
    };
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    [JsonIgnore] private string TitleFormatted => $"<b>{Title}</b>{(string.IsNullOrEmpty(Subtitle) ? "" : " <size=75%><alpha=#AA>" + Subtitle)}";
    public string Artist { get; set; } = string.Empty;
    public string Mapper { get; set; } = string.Empty;
    public int? SpinShareKey { get; set; }
    public string Requester { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public int? EasyRating { get; set; }
    public int? NormalRating { get; set; }
    public int? HardRating { get; set; }
    public int? ExpertRating { get; set; }
    // ReSharper disable InconsistentNaming
    public int? XDRating { get; set; }
    // SpinShare does not support listing RemiXD ratings, so this will always be null unless it's being generated with in-game data
    public int? RemiXDRating { get; set; }
    // ReSharper restore InconsistentNaming
    public string? ActiveDifficulty { get; set; }
    public Dictionary<string, int?> DifficultyAsDictionary() {
        return new Dictionary<string, int?>
        {
            {"Easy", EasyRating},
            {"Normal", NormalRating},
            {"Hard", HardRating},
            {"Expert", ExpertRating},
            {"XD", XDRating},
        };
    }
    public bool AlreadyDownloaded
    {
        get
        {
            string path = Path.Combine(Plugin.CustomsPath, $"{FileReference}.srtb");
            
            if (FileReference == null || !File.Exists(path))
            {
                return false;
            }

            return UpdateDateTime == null || File.GetLastWriteTime(path) >= UpdateDateTime;
        }
    }

    public string? FileReference { get; set; } = string.Empty;
    public long? UploadTime { get; set; }
    [JsonIgnore] private DateTime? UpdateDateTime { get; set; }
    public long? UpdateTime => UpdateDateTime == null ? null : DateTimeOffset.FromFileTime(UpdateDateTime.Value.ToFileTime()).ToUnixTimeSeconds();
    public bool HasPlayed => FileReference != null && Plugin.PlayedMapHistory.Any(x => x.FileReference == FileReference);
    public bool InQueue => FileReference != null && QueueList.Entries.Concat(QueueList.BufferedList).Any(x => x.FileReference == FileReference);
    // ReSharper restore UnusedAutoPropertyAccessor.Global
    // ReSharper restore MemberCanBePrivate.Global
    
    public QueueEntry(SongDetail details, Dictionary<string, string>? query = null)
    {
        Title = details.title;
        Subtitle = details.subtitle;
        Artist = details.artist;
        Mapper = details.charter;
        SpinShareKey = details.id;
        FileReference = details.fileReference;
        
        // just checking to see if details.xxxxDifficulty is null isn't enough, sometimes seems to just show 0 if not present on the API
        // so we check it with the bool also in the data lol
        EasyRating = details.hasEasyDifficulty ? details.easyDifficulty : null;
        NormalRating = details.hasNormalDifficulty ? details.normalDifficulty : null;
        HardRating = details.hasHardDifficulty ? details.hardDifficulty : null;
        ExpertRating = details.hasExpertDifficulty ? details.expertDifficulty : null;
        XDRating = details.hasXDDifficulty ? details.XDDifficulty : null;
        
        // details.uploadDate.stimezone is null (erm), but SpinShare stores time in Europe/Berlin
        // https://github.com/unicode-org/cldr/blob/59dfe3ad9720e304957658bd991df8b0dba3519a/common/supplemental/windowsZones.xml#L307
        DateTime uploadDateTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(details.uploadDate.date, "W. Europe Standard Time", TimeZoneInfo.Local.Id);
        UploadTime = DateTimeOffset.FromFileTime(uploadDateTime.ToFileTime()).ToUnixTimeSeconds();
        if (details.updateDate != null)
        {
            UpdateDateTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(details.updateDate.date, "W. Europe Standard Time", TimeZoneInfo.Local.Id);
        }

        if (query == null)
        {
            return;
        }
        
        if (query.TryGetValue("user", out string? user))
        { 
            Requester = user;
        }
        if (query.TryGetValue("service", out string? service))
        { 
            Service = service;
        }
    }
    public QueueEntry(PlayableTrackData trackData)
    {
        TrackInfoMetadata metadata = trackData.Setup.TrackDataSegmentForSingleTrackDataSetup.metadata.TrackInfoMetadata;
        MetadataHandle metadataHandle = trackData.Setup.TrackDataSegmentForSingleTrackDataSetup.metadata;
        
        Title = metadata.title;
        Subtitle = metadata.subtitle;
        Artist = metadata.artistName;
        Mapper = metadata.charter;
        
        string? reference = metadataHandle.UniqueName;
        if (!string.IsNullOrEmpty(reference))
        {
            if (reference.LastIndexOf('_') != -1)
            {
                reference = reference.Remove(metadataHandle.UniqueName.LastIndexOf('_')).Replace("CUSTOM_", string.Empty);
            }
        }
        FileReference = reference;
        
        ActiveDifficulty = trackData.Difficulty.ToString();
        // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
        switch (trackData.Difficulty)
        {
            case TrackData.DifficultyType.Easy:
                EasyRating = trackData.DifficultyRating;
                break;
            case TrackData.DifficultyType.Normal:
                NormalRating = trackData.DifficultyRating;
                break;
            case TrackData.DifficultyType.Hard:
                HardRating = trackData.DifficultyRating;
                break;
            case TrackData.DifficultyType.Expert:
                ExpertRating = trackData.DifficultyRating;
                break;
            case TrackData.DifficultyType.XD:
                XDRating = trackData.DifficultyRating;
                break;
            case TrackData.DifficultyType.RemiXD:
                RemiXDRating = trackData.DifficultyRating;
                break;
        }
    }
    public QueueEntry() { }

    private async Task PlayButtonPressed()
    {
        await Awaitable.MainThreadAsync();
                
        Plugin.Log.LogDebug($"PLAY -- {Title} ({SpinShareKey})");
        
        XDSelectionListMenu.Instance.ClearSearch();
        
        XDSelectionListItemDisplay_TabPanel[] filtersTabPanel = Object.FindObjectsByType<XDSelectionListItemDisplay_TabPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None) ?? [];
        if (filtersTabPanel.Length <= 0)
        {
            Plugin.Log.LogInfo("erm... filters?????");
        }
        else
        {
            // ReSharper disable once SimplifyLinqExpressionUseAll
            if (!filtersTabPanel.Any(x => x.gameObject.name == "TabPanel_TrackFilter(Clone)"))
            {
                GameObject.Find("Dot Selector Button TrackFilter")?.GetComponent<XDNavigableButton>().onClick.Invoke();
                GameObject.Find("Dot Selector Button QueueListPanel")?.GetComponent<XDNavigableButton>().onClick.Invoke(); // i... sigh
            }
            
            filtersTabPanel
                .First(x => x.gameObject.name == "TabPanel_TrackFilter(Clone)").transform
                .Find("Scroll List Tab Prefab/Scroll View/Viewport/Content/FilterSettingsPopout")
                .GetComponent<XDOptionValueResetGroup>().SetToDefaults();
        }

        if (!AlreadyDownloaded)
        {
            WorldMenuGameState? worldMenuGameState = Object.FindAnyObjectByType<WorldMenuGameState>();
            Transform? levelSelectTransform = worldMenuGameState?.transform.Find("LevelSelect");
            GameState? levelSelectGameState = levelSelectTransform?.GetComponent<GameState>();
            if (levelSelectGameState != null)
            {
                if (!levelSelectGameState.ShouldBeActive)
                {
                    // this navigates the player back to the map selection menu too
                    XDSelectionListMenu.Instance.ScrollToTrack(PlayerSettingsData.Instance.LastPlayedTrackHandle);
                }
            }
            else
            {
                Plugin.Log.LogWarning("uh levelSelectGameState is null?");
            }

            NotificationSystemGUI.AddMessage($"Downloading map {SpinShareKey}...", 5f);
            
            await Plugin.SpinShare.downloadSongAndUnzip(SpinShareKey.ToString(), Plugin.CustomsPath);
            XDSelectionListMenu.Instance.FireRapidTrackDataChange();
            
            NotificationSystemGUI.AddMessage($"Successfully downloaded map {SpinShareKey}!");
        }

        int attempts = 0;
        MetadataHandle metadataHandle;
        
        keepTrying:
            try
            {
                metadataHandle = XDSelectionListMenu.Instance._sortedTrackList.First(handle =>
                {
                    if (string.IsNullOrEmpty(handle.UniqueName))
                    {
                        return false;
                    }
                    
                    string reference = handle.UniqueName;
                    if (reference.LastIndexOf('_') != -1)
                    {
                        reference = reference.Remove(handle.UniqueName.LastIndexOf('_'));
                    }

                    return FileReference == reference.Replace("CUSTOM_", string.Empty);
                });
            }
            catch (Exception innerException)
            {
                if (innerException is not InvalidOperationException)
                {
                    throw;
                }
                
                attempts++;
                if (attempts >= 12)
                {
                    Plugin.Log.LogError($"Failed to find map {SpinShareKey}");
                    throw;
                }
                await Task.Delay(250);
                goto keepTrying;
            }

#if DEBUG
        Plugin.Log.LogInfo($"Found map {SpinShareKey} after {attempts} attempts");
#endif
        
        XDSelectionListMenu.Instance.ScrollToTrack(metadataHandle);
        QueueList.Entries.Remove(this);
        QueueList.CheckIndicatorDot();
        QueueList.SavePersistentQueue();
        SocketApi.Broadcast("Played", this);
    }

    private void SkipButtonPressed()
    {
        Plugin.Log.LogDebug($"SKIP -- {Title} ({SpinShareKey})");
        QueueList.Entries.Remove(this);
        QueueList.CheckIndicatorDot();
        QueueList.SavePersistentQueue();
        SocketApi.Broadcast("Skipped", this);
    }

    public async Task AddToQueue(bool silent = false)
    {
        // a bunch of UI functions happen here and Unity gets Very Very Angry if we're not on the main thread
        await Awaitable.MainThreadAsync();

        if (!silent && Plugin.EnableQueueNotifications.Value)
        {
            NotificationSystemGUI.AddMessage(
                $"<b>{Requester}</b> added <i>{Title}</i> <alpha=#AA>({SpinShareKey})<alpha=#FF> to the queue!", 7f);
        }

        if (QueueList.QueueListContainer == null)
        {
            QueueList.BufferedList.Add(this);
            QueueList.CheckIndicatorDot();
            return;
        }
        
        CustomGroup entryGroup = UIHelper.CreateGroup(QueueList.QueueListContainer, "QueueEntry");
        entryGroup.Transform.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 10, 10);
        
        CustomGroup displayGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryDisplay", Axis.Horizontal);
        displayGroup.Transform.GetComponent<HorizontalLayoutGroup>().spacing = 10f;
        
        #region art
        // web requests to file:// are just easier and i'm all about easy
        UnityWebRequest request = UnityWebRequestTexture.GetTexture(AlreadyDownloaded
            ? $"file://{Plugin.CustomsPath}/AlbumArt/{FileReference}.png"
            : $"https://spinsha.re/uploads/thumbnail/{FileReference}.jpg");
        UnityWebRequestAsyncOperation response = request.SendWebRequest();

        response.completed += async _ =>
        {
            await Awaitable.MainThreadAsync();
            
            Texture2D texture = DownloadHandlerTexture.GetContent(request);
            CustomImage artImage = UIHelper.CreateImage(displayGroup, "QueueEntryArt", texture);
            artImage.Transform.SetSiblingIndex(0);
            
            artImage.Transform.GetChild(0).GetComponent<RectTransform>().sizeDelta = new Vector2(110, 110);

            artImage.Transform.GetComponent<LayoutElement>().preferredHeight = 100;
            artImage.Transform.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
        };
        #endregion

        #region metadata
        CustomGroup metadataGroup = UIHelper.CreateGroup(displayGroup, "QueueEntryMetadata");
        VerticalLayoutGroup metadataLayoutGroupComponent = metadataGroup.Transform.GetComponent<VerticalLayoutGroup>();
        metadataLayoutGroupComponent.spacing = 0;
        
        CustomTextComponent entryTitle = UIHelper.CreateLabel(metadataGroup, "QueueEntryTitle", TranslationReference.Empty);
        LayoutElement entryTitleLayoutComponent = entryTitle.Transform.GetComponent<LayoutElement>();
        entryTitleLayoutComponent.preferredWidth = 350;
        CustomTextMeshProUGUI entryTitleTextComponent = entryTitle.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryTitleTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryTitleTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryTitle.ExtraText = TitleFormatted;
        
        CustomTextComponent entryArtist = UIHelper.CreateLabel(metadataGroup, "QueueEntryArtist", TranslationReference.Empty);
        LayoutElement entryArtistLayoutComponent = entryArtist.Transform.GetComponent<LayoutElement>();
        entryArtistLayoutComponent.preferredWidth = 350;
        CustomTextMeshProUGUI entryArtistTextComponent = entryArtist.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryArtistTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryArtistTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryArtistTextComponent.fontSize = 24;
        entryArtistTextComponent.fontStyle = FontStyles.Italic;
        entryArtist.ExtraText = $"<alpha=#AA>by <alpha=#FF>{Artist}";
        
        CustomTextComponent entryMapper = UIHelper.CreateLabel(metadataGroup, "QueueEntryMapper", TranslationReference.Empty);
        LayoutElement entryMapperLayoutComponent = entryMapper.Transform.GetComponent<LayoutElement>();
        entryMapperLayoutComponent.preferredWidth = 350;
        CustomTextMeshProUGUI entryMapperTextComponent = entryMapper.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryMapperTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryMapperTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryMapperTextComponent.fontSize = 24;
        entryMapper.ExtraText = $"<alpha=#AA>charted by <alpha=#FF>{Mapper}";
        #endregion
        
        #region difficulty labels
        CustomGroup diffLabelGroup = UIHelper.CreateGroup(entryGroup, "QueueDiffLabels", Axis.Horizontal);
        diffLabelGroup.Transform.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
        
        Dictionary<string, int?> diffDict = DifficultyAsDictionary();
        Dictionary<string, CustomButton> diffLabels = new()
        {
            { "Easy", UIHelper.CreateButton(diffLabelGroup, "EasyDiffLabel", TranslationReference.Empty, () => { }) },
            { "Normal", UIHelper.CreateButton(diffLabelGroup, "NormalDiffLabel", TranslationReference.Empty, () => { }) },
            { "Hard", UIHelper.CreateButton(diffLabelGroup, "HardDiffLabel", TranslationReference.Empty, () => { }) },
            { "Expert", UIHelper.CreateButton(diffLabelGroup, "ExpertDiffLabel", TranslationReference.Empty, () => { }) },
            { "XD", UIHelper.CreateButton(diffLabelGroup, "XDDiffLabel", TranslationReference.Empty, () => { }) }
        };
        foreach (string diff in diffLabels.Keys)
        {
            CustomButton label = diffLabels[diff];
            label.RemoveAllListeners();
            
            LayoutElement layoutElement = label.Transform.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 50;
            
            XDNavigable navigable = label.Transform.GetComponent<XDNavigable>();
            navigable.forceExpanded = diffDict[diff] != null;
            navigable.navigable = false;
            navigable.canBeDefaultNavigable = false;
            navigable.selectOnHover = false;
            navigable.glyphType = XDNavigable.GlyphType.None;
            
            CanvasGroup canvasGroup = label.Transform.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            
            XDNavigableButton navigableButton = label.Transform.GetComponent<XDNavigableButton>();
            navigableButton.interactable = false;
            
            CustomTextMeshProUGUI labelTextComponent = label.Transform.Find("IconContainer/ButtonText").GetComponent<CustomTextMeshProUGUI>();
            labelTextComponent.richText = true;
            labelTextComponent.fontSizeMin = 24;
            labelTextComponent.fontSizeMax = 24;
            labelTextComponent.fontStyle = FontStyles.Normal;
            
            TranslatedTextMeshPro translatedTextMeshPro = label.Transform.Find("IconContainer/ButtonText").GetComponent<TranslatedTextMeshPro>();
            translatedTextMeshPro.TextToAppend = $"{DifficultyAbbreviations[diff]} <space=0.2em> <b>" + (diffDict[diff] == null ? "-" : diffDict[diff].ToString()) + "</b>";
        }
        #endregion
        
        #region buttons
        CustomGroup buttonGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryButtons", Axis.Horizontal);
        
        // lambda moment smh
        CustomButton playButton = UIHelper.CreateButton(buttonGroup, "PlayButton", "SpinRequests_PlayButtonText", async void () =>
        {
            try
            {
                await PlayButtonPressed();
                Object.DestroyImmediate(entryGroup.GameObject);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        });
        playButton.Transform.GetComponent<LayoutElement>().preferredWidth = 200;
        playButton.Transform.GetComponent<XDNavigable>().forceExpanded = true;
        
        UIHelper.CreateButton(buttonGroup, "SkipButton", "SpinRequests_SkipButtonText", () =>
        {
            SkipButtonPressed();
            Object.DestroyImmediate(entryGroup.GameObject);
        });
        #endregion
        
        QueueList.Entries.Add(this);
        QueueList.CheckIndicatorDot();
        QueueList.SavePersistentQueue();
        
#if RELEASE
        if (!silent)
        {
            SocketApi.Broadcast("AddedToQueue", this);
        }
#else
        SocketApi.Broadcast("AddedToQueue", this);
#endif
    }
}