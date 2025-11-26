using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public int? Duration { get; set; }
    public int? SpinShareKey { get; set; }
    public string? NonCustomId { get; set; }
    public bool IsCustom { get; set; }
    public string Requester { get; set; } = string.Empty;
    public long? RequestedAt { get; set; }
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
            if (!IsCustom)
            {
                return true;
            }
            
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
    public bool HasPlayed => FileReference != null && Plugin.MapsThatCrossedPlayedThreshold.Contains(FileReference);
    public bool InQueue => FileReference != null && QueueList.Entries.Concat(QueueList.BufferedList).Any(x => x.FileReference == FileReference);
    [JsonIgnore] private CustomButton? _playButton;
    [JsonIgnore] private CustomTextComponent? _entryRequester;
    // ReSharper restore UnusedAutoPropertyAccessor.Global
    // ReSharper restore MemberCanBePrivate.Global
    
    [JsonIgnore] private readonly MetadataHandle? _handle;

    private void SetQueryDetails(Dictionary<string, string>? query = null)
    {
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
    
    public QueueEntry(SongDetail details, Dictionary<string, string>? query = null)
    {
        IsCustom = true;
        
        Title = details.title;
        Subtitle = details.subtitle;
        Artist = details.artist;
        Mapper = details.charter;
        SpinShareKey = details.id;
        FileReference = details.fileReference;
        RequestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
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

        if (AlreadyDownloaded)
        {
            try
            {
                Duration = Mathf.FloorToInt(XDSelectionListMenu.Instance._sortedTrackList
                    .First(x => GetSafeReference(x.UniqueName) == FileReference)
                    .GetClosestTrackData(TrackData.DifficultyType.XD, IntRange.FromStartAndCount(0, 255)).Duration);
            }
            catch (Exception e)
            {
                if (e is not InvalidOperationException)
                {
                    throw;
                }
                
                // otherwise, ignore it. happens when the list hasn't loaded yet
            }
        }
        
        SetQueryDetails(query);
    }
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    // where in the flippity frick do i find these >:(
    internal enum DlcAbbreviations
    {
        BG = 0, // Base game
        MC = 1000, // Monstercat
        CH = 2000, // Chillhop
        SP = 3000, // Supporter Pack
        IN = 4000 // Indie Pack
    }
    private readonly Dictionary<DlcAbbreviations, string> _dlcNames = new()
    {
        { DlcAbbreviations.BG, "Base Game" },
        { DlcAbbreviations.MC, "Monstercat DLC" },
        { DlcAbbreviations.CH, "Chillhop DLC" },
        { DlcAbbreviations.SP, "Supporter Pack DLC" },
        { DlcAbbreviations.IN, "Indie Pack DLC" }
    };

    private static string GetFileReference(MetadataHandle metadataHandle)
    {
        string? reference = metadataHandle.UniqueName;
        if (string.IsNullOrEmpty(reference))
        {
            return reference;
        }
        
        if (reference.LastIndexOf('_') != -1)
        {
            reference = reference.Remove(metadataHandle.UniqueName.LastIndexOf('_')).Replace("CUSTOM_", string.Empty);
        }
        
        return reference;
    }

    public QueueEntry(MetadataHandle metadataHandle, Dictionary<string, string>? query = null)
    {
        _handle = metadataHandle;
        TrackInfoMetadata metadata = metadataHandle.TrackInfoMetadata;
        
        Title = metadata.title;
        Subtitle = metadata.subtitle;
        Artist = $"{metadata.artistName}{(string.IsNullOrEmpty(metadata.featArtists) ? "" : $" {metadata.featArtists}")}";
        Mapper = metadata.charter;
        Duration = Mathf.FloorToInt(metadataHandle.GetClosestTrackData(TrackData.DifficultyType.XD, IntRange.FromStartAndCount(0, 255)).Duration);
        NonCustomId = $"{(DlcAbbreviations)metadata.trackOrder - (metadata.trackOrder % 1000)}{metadata.trackOrder % 1000}";
        FileReference = GetFileReference(metadataHandle);
        IsCustom = metadata.isCustom;
        RequestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        TrackDataMetadata? easyInfo = metadataHandle.TrackDataMetadata.GetMetadataForDifficulty(TrackData.DifficultyType.Easy);
        EasyRating = easyInfo?.DifficultyRating;
        TrackDataMetadata? normalInfo = metadataHandle.TrackDataMetadata.GetMetadataForDifficulty(TrackData.DifficultyType.Normal);
        NormalRating = normalInfo?.DifficultyRating;
        TrackDataMetadata? hardInfo = metadataHandle.TrackDataMetadata.GetMetadataForDifficulty(TrackData.DifficultyType.Hard);
        HardRating = hardInfo?.DifficultyRating;
        TrackDataMetadata? expertInfo = metadataHandle.TrackDataMetadata.GetMetadataForDifficulty(TrackData.DifficultyType.Expert);
        ExpertRating = expertInfo?.DifficultyRating;
        TrackDataMetadata? xdInfo = metadataHandle.TrackDataMetadata.GetMetadataForDifficulty(TrackData.DifficultyType.XD);
        XDRating = xdInfo?.DifficultyRating;
        TrackDataMetadata? remixdInfo = metadataHandle.TrackDataMetadata.GetMetadataForDifficulty(TrackData.DifficultyType.RemiXD);
        RemiXDRating = remixdInfo?.DifficultyRating;
        
        SetQueryDetails(query);
    }
    
    public QueueEntry(PlayableTrackData trackData)
    {
        TrackInfoMetadata metadata = trackData.Setup.TrackDataSegmentForSingleTrackDataSetup.metadata.TrackInfoMetadata;
        MetadataHandle metadataHandle = trackData.Setup.TrackDataSegmentForSingleTrackDataSetup.metadata;
        _handle = metadataHandle;
        
        Title = metadata.title;
        Subtitle = metadata.subtitle;
        Artist = $"{metadata.artistName}{(string.IsNullOrEmpty(metadata.featArtists) ? "" : $" {metadata.featArtists}")}";
        Mapper = metadata.charter;
        Duration = Mathf.FloorToInt(trackData.Setup.TrackDataSegmentForSingleTrackDataSetup.GetTrackDataMetadata().Duration);
        NonCustomId = $"{(DlcAbbreviations)metadata.trackOrder - (metadata.trackOrder % 1000)}{metadata.trackOrder % 1000}";
        FileReference = GetFileReference(metadataHandle);
        IsCustom = metadata.isCustom;
        
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

    private static string GetSafeReference(string reference)
    {
        if (reference.LastIndexOf('_') != -1)
        {
            reference = reference.Remove(reference.LastIndexOf('_'));
        }

        return reference.Replace("CUSTOM_", string.Empty);
    }

    private async Task<bool> PlayButtonPressed()
    {
        await Awaitable.MainThreadAsync();
                
        Plugin.Log.LogDebug($"PLAY -- {Title} ({SpinShareKey?.ToString() ?? NonCustomId})");
        
        XDSelectionListMenu.Instance.ClearSearch();
        PlayerSettingsData.Instance.FilterCustomTracks.ResetData();
        PlayerSettingsData.Instance.FilterMaximumDifficulty.ResetData();
        PlayerSettingsData.Instance.FilterMinimumDifficulty.ResetData();
        PlayerSettingsData.Instance.ShowOnlyFavouritesArcade.ResetData();

        XDNavigable navigable = _playButton!.Transform.GetComponent<XDNavigable>();
        XDNavigableButton navigableButton = _playButton!.Transform.GetComponent<XDNavigableButton>();

        bool previousDownloadedState = AlreadyDownloaded;

        if (!AlreadyDownloaded && NonCustomId is null or "BG0")
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
            
            navigable.forceExpanded = false;
            navigable.navigable = false;
            navigableButton.interactable = false;
            _playButton!.TextTranslationKey = "SpinRequests_DownloadingButtonText";

            string srtbFilename = Path.Combine(Plugin.CustomsPath, $"{FileReference}.srtb");
            string artFilename = Path.Combine(Plugin.CustomsPath, $"AlbumArt/{FileReference}.png");
            long unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (File.Exists(srtbFilename))
            {
                if (Plugin.DeleteOldMapFiles.Value)
                {
                    File.Delete(srtbFilename);
                }
                else
                {
                    File.Move(srtbFilename,
                        Path.Combine(Plugin.CustomsPath, $"{FileReference}old_{unixTimestamp}.srtb"));
                }
            }
            if (File.Exists(artFilename))
            {
                if (Plugin.DeleteOldMapFiles.Value)
                {
                    File.Delete(artFilename);
                }
                else
                {
                    File.Move(artFilename,
                        Path.Combine(Plugin.CustomsPath, $"AlbumArt/{FileReference}old_{unixTimestamp}.png"));
                }
            }

            if (await Plugin.SpinShare.downloadSongAndUnzip(SpinShareKey.ToString(), Plugin.CustomsPath))
            {
                // to explain the weird flow decision here, and why the success message sometimes never shows:
                // there's some weird exception being thrown in downloadSongAndUnzip and since it's not re-throwing it, i've got no idea what's going wrong
                // but it *does* successfully download anyways. so idk
                
                NotificationSystemGUI.AddMessage($"Successfully downloaded map {SpinShareKey}!");
            }
            XDSelectionListMenu.Instance.FireRapidTrackDataChange();
        }

        int attempts = 0;
        MetadataHandle metadataHandle;
        
        keepTrying:
            try
            {
                
                metadataHandle = _handle ?? XDSelectionListMenu.Instance._sortedTrackList.First(handle =>
                {
                    if (string.IsNullOrEmpty(handle.UniqueName))
                    {
                        return false;
                    }

                    if (handle.UniqueName.Contains("old_"))
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
                    FailedDownloadingMap($"Failed downloading map {SpinShareKey} (wuh oh)");
                    throw;
                }
                
                attempts++;
                if (attempts >= 12)
                {
                    FailedDownloadingMap($"Failed to find map {SpinShareKey?.ToString() ?? NonCustomId}");
                    throw;
                }
                await Task.Delay(250);
                goto keepTrying;
            }

#if DEBUG
        Plugin.Log.LogInfo($"Found map {SpinShareKey?.ToString() ?? NonCustomId} after {attempts} attempts");
#endif

        if (!previousDownloadedState)
        {
            if (Plugin.JumpToMapAfterDownloading.Value)
            {
                goto finished;
            }

            _playButton!.TextTranslationKey = "SpinRequests_PlayButtonText";
            Duration = Mathf.FloorToInt(metadataHandle.GetClosestTrackData(TrackData.DifficultyType.XD, IntRange.FromStartAndCount(0, 255)).Duration);
            if (Duration != null)
            {
                _playButton.ExtraText = $" <alpha=#AA>({Duration.Value / 60}:{(Duration.Value % 60).ToString().PadLeft(2, '0')})";
            }
            
            navigable.forceExpanded = true;
            navigable.navigable = true;
            navigableButton.interactable = true;
            
            QueueList.SavePersistentQueue();
            
            return false;
        }

        finished:
            XDSelectionListMenu.Instance.ScrollToTrack(metadataHandle);
            QueueList.Entries.Remove(this);
            QueueList.CheckIndicatorDot();
            SocketApi.Broadcast("Played", this);
            return true;
    }

    private void FailedDownloadingMap(string? message = null)
    {
        _playButton!.Transform.GetComponent<XDNavigable>().forceExpanded = true;
        _playButton!.Transform.GetComponent<XDNavigable>().navigable = true;
        _playButton!.Transform.GetComponent<XDNavigableButton>().interactable = true;
        _playButton!.TextTranslationKey = "SpinRequests_DownloadButtonText";

        if (message != null)
        {
            NotificationSystemGUI.AddMessage(message, 5f);
        }
    }

    private void SkipButtonPressed()
    {
        Plugin.Log.LogDebug($"SKIP -- {Title} ({SpinShareKey?.ToString() ?? NonCustomId})");
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
                $"<b>{Requester}</b> added <i>{Title}</i> <alpha=#AA>({SpinShareKey?.ToString() ?? NonCustomId})<alpha=#FF> to the queue!", 7f);
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

        void SetArt(Texture2D texture)
        {
            CustomImage artImage = UIHelper.CreateImage(displayGroup, "QueueEntryArt", texture);
            artImage.Transform.SetSiblingIndex(0);

            artImage.Transform.GetChild(0).GetComponent<RectTransform>().sizeDelta = new Vector2(110, 110);

            artImage.Transform.GetComponent<LayoutElement>().preferredHeight = 100;
            artImage.Transform.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);
        }
        
        if (!IsCustom)
        {
            await Awaitable.MainThreadAsync();
            SetArt(_handle!.albumArtRef.asset);
        }
        else
        {
            // web requests to file:// are just easier and i'm all about easy
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(AlreadyDownloaded
                ? $"file://{Plugin.CustomsPath}/AlbumArt/{FileReference}.png"
                : $"https://spinsha.re/uploads/thumbnail/{FileReference}.jpg");
            UnityWebRequestAsyncOperation response = request.SendWebRequest();

            response.completed += async _ =>
            {
                await Awaitable.MainThreadAsync();

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                SetArt(texture);
            };
        }

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
        if (!IsCustom)
        {
            if (Enum.TryParse(NonCustomId!.Substring(0, 2), true, out DlcAbbreviations abbreviation))
            {
                entryMapper.ExtraText = $"<alpha=#AA>from the <alpha=#FF>{_dlcNames[abbreviation]}";   
            }
        }
        else
        {
            entryMapper.ExtraText = $"<alpha=#AA>charted by <alpha=#FF>{Mapper}";
        }

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
        
        #region requester
        if (!string.IsNullOrEmpty(Requester))
        {
            CustomGroup requesterGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryRequester");
            VerticalLayoutGroup requesterLayoutGroupComponent = requesterGroup.Transform.GetComponent<VerticalLayoutGroup>();
            requesterLayoutGroupComponent.spacing = 0;

            _entryRequester = UIHelper.CreateLabel(requesterGroup, "QueueEntryRequester", TranslationReference.Empty);
            CustomTextMeshProUGUI entryRequesterTextComponent = _entryRequester.Transform.GetComponent<CustomTextMeshProUGUI>();
            entryRequesterTextComponent.alignment = TextAlignmentOptions.Center;
            entryRequesterTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
            entryRequesterTextComponent.overflowMode = TextOverflowModes.Ellipsis;
            entryRequesterTextComponent.fontSize = 24;
            entryRequesterTextComponent.fontStyle = FontStyles.Italic;
            UpdateRequesterInformation();
        }
        #endregion
        
        #region buttons
        CustomGroup buttonGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryButtons", Axis.Horizontal);
        
        // lambda moment smh
        _playButton = UIHelper.CreateButton(buttonGroup, "PlayButton",
            $"SpinRequests_{(AlreadyDownloaded ? "PlayButtonText" : "DownloadButtonText")}", async void () =>
        {
            try
            {
                if (await PlayButtonPressed())
                {
                    Object.Destroy(entryGroup.GameObject);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        });
        _playButton.Transform.GetComponent<LayoutElement>().preferredWidth = 200;
        _playButton.Transform.GetComponent<XDNavigable>().forceExpanded = true;
        if (Duration != null)
        {
            _playButton.ExtraText = $" <alpha=#AA>({Duration.Value / 60}:{(Duration.Value % 60).ToString().PadLeft(2, '0')})";
        }
        
        UIHelper.CreateButton(buttonGroup, "SkipButton", "SpinRequests_SkipButtonText", () =>
        {
            SkipButtonPressed();
            Object.Destroy(entryGroup.GameObject);
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

    internal void UpdateRequesterInformation()
    {
        if (string.IsNullOrEmpty(Requester) || _entryRequester == null)
        {
            return;
        }

        long? mins = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - RequestedAt) / 60;
        _entryRequester.ExtraText =
            $"<alpha=#AA>requested by <alpha=#FF>{Requester} <alpha=#AA>{(mins switch
            {
                0 => "right now",
                1 => "1 minute ago",
                >= 120 => "a while ago", // ...i can do that??? cool
                _ => $"{mins} minutes ago"
            })}";
    }
}