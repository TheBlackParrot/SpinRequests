using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpinCore.UI;
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
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable UnusedAutoPropertyAccessor.Global
    public string Title { get; set; } = string.Empty;
    [JsonIgnore] private string TitleFormatted { get; set; } = string.Empty;
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
    public bool AlreadyDownloaded => FileReference == null || File.Exists(Path.Combine(Plugin.CustomsPath, $"{FileReference}.srtb"));
    public string? FileReference { get; set; } = string.Empty;
    public long? UploadTime { get; set; }
    // ReSharper restore UnusedAutoPropertyAccessor.Global
    // ReSharper restore MemberCanBePrivate.Global
    
    public QueueEntry(SongDetail details, Dictionary<string, string>? query = null)
    {
        Title = $"{details.title}{(string.IsNullOrEmpty(details.subtitle) ? "" : " - " + details.subtitle)}";
        TitleFormatted = $"<b>{details.title}</b>{(string.IsNullOrEmpty(details.subtitle) ? "" : " <size=75%><alpha=#AA>" + details.subtitle)}";
        Artist = details.artist;
        Mapper = details.charter;
        SpinShareKey = details.id;
        FileReference = details.fileReference;
        
        EasyRating = details.easyDifficulty;
        NormalRating = details.normalDifficulty;
        HardRating = details.hardDifficulty;
        ExpertRating = details.expertDifficulty;
        XDRating = details.XDDifficulty;
        
        // details.uploadDate.stimezone is null (erm), but SpinShare stores time in Europe/Berlin
        // https://github.com/unicode-org/cldr/blob/59dfe3ad9720e304957658bd991df8b0dba3519a/common/supplemental/windowsZones.xml#L307
        DateTime localTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(details.uploadDate.date, "W. Europe Standard Time", TimeZoneInfo.Local.Id);
        UploadTime = DateTimeOffset.FromFileTime(localTime.ToFileTime()).ToUnixTimeSeconds();

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
        EasyRating = trackData.Difficulty == TrackData.DifficultyType.Easy ? trackData.DifficultyRating : null;
        NormalRating = trackData.Difficulty == TrackData.DifficultyType.Normal ? trackData.DifficultyRating : null;
        HardRating = trackData.Difficulty == TrackData.DifficultyType.Hard ? trackData.DifficultyRating : null;
        ExpertRating = trackData.Difficulty == TrackData.DifficultyType.Expert ? trackData.DifficultyRating : null;
        XDRating = trackData.Difficulty == TrackData.DifficultyType.XD ? trackData.DifficultyRating : null;
        RemiXDRating = trackData.Difficulty == TrackData.DifficultyType.RemiXD ? trackData.DifficultyRating : null;
    }
    public QueueEntry() { }

    public async Task AddToQueue(bool silent = false)
    {
        // a bunch of UI functions happen here and Unity gets Very Very Angry if we're not on the main thread
        await Awaitable.MainThreadAsync();

        if (!silent)
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
        
        CustomGroup displayGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryDisplay", Axis.Horizontal);
        
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
        entryTitleLayoutComponent.preferredWidth = 300;
        CustomTextMeshProUGUI entryTitleTextComponent = entryTitle.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryTitleTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryTitleTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryTitle.ExtraText = TitleFormatted;
        
        CustomTextComponent entryArtist = UIHelper.CreateLabel(metadataGroup, "QueueEntryArtist", TranslationReference.Empty);
        LayoutElement entryArtistLayoutComponent = entryArtist.Transform.GetComponent<LayoutElement>();
        entryArtistLayoutComponent.preferredWidth = 300;
        CustomTextMeshProUGUI entryArtistTextComponent = entryArtist.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryArtistTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryArtistTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryArtistTextComponent.fontSize = 24;
        entryArtistTextComponent.fontStyle = FontStyles.Italic;
        entryArtist.ExtraText = $"<alpha=#AA>by <alpha=#FF>{Artist}";
        
        CustomTextComponent entryMapper = UIHelper.CreateLabel(metadataGroup, "QueueEntryMapper", TranslationReference.Empty);
        LayoutElement entryMapperLayoutComponent = entryMapper.Transform.GetComponent<LayoutElement>();
        entryMapperLayoutComponent.preferredWidth = 300;
        CustomTextMeshProUGUI entryMapperTextComponent = entryMapper.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryMapperTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryMapperTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryMapperTextComponent.fontSize = 24;
        entryMapper.ExtraText = $"<alpha=#AA>charted by <alpha=#FF>{Mapper}";
        #endregion
        
        #region buttons
        CustomGroup buttonGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryButtons", Axis.Horizontal);
        
        // lambda moment smh
        UIHelper.CreateButton(buttonGroup, "PlayButton", "SpinRequests_PlayButtonText", async void () =>
        {
            try
            {
                await Awaitable.MainThreadAsync();
                
                Plugin.Log.LogDebug($"PLAY -- {Title} ({SpinShareKey})");

                if (!AlreadyDownloaded)
                {
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
                Object.DestroyImmediate(entryGroup.GameObject);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError(e);
            }
        });
        UIHelper.CreateButton(buttonGroup, "SkipButton", "SpinRequests_SkipButtonText", () =>
        {
            Plugin.Log.LogDebug($"SKIP -- {Title} ({SpinShareKey})");
            QueueList.Entries.Remove(this);
            QueueList.CheckIndicatorDot();
            Object.DestroyImmediate(entryGroup.GameObject);
        });
        #endregion
        
        QueueList.Entries.Add(this);
        QueueList.CheckIndicatorDot();
    }
}