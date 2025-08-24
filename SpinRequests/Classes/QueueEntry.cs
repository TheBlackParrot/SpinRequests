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
using Object = UnityEngine.Object;

namespace SpinRequests.Classes;

public class QueueEntry
{
    public string Title { get; set; } = string.Empty;
    [JsonIgnore] private string TitleFormatted { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Mapper { get; set; } = string.Empty;
    public int? SpinShareKey { get; set; } = null;
    public string Requester { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    [JsonIgnore] private string? FileReference { get; set; } = string.Empty;
    
    public QueueEntry(SongDetail details, Dictionary<string, string>? query = null)
    {
        Title = $"{details.title}{(string.IsNullOrEmpty(details.subtitle) ? "" : " - " + details.subtitle)}";
        TitleFormatted = $"<b>{details.title}</b>{(string.IsNullOrEmpty(details.subtitle) ? "" : " <size=75%><alpha=#AA>" + details.subtitle)}";
        Artist = details.artist;
        Mapper = details.charter;
        SpinShareKey = details.id;
        FileReference = details.fileReference;

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
    public QueueEntry() { }

    private bool AlreadyDownloaded => FileReference == null || File.Exists(Path.Combine(Plugin.CustomsPath, $"{FileReference}.srtb"));

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
            return;
        }
        
        CustomGroup entryGroup = UIHelper.CreateGroup(QueueList.QueueListContainer, "QueueEntry");
        
        #region metadata
        CustomGroup metadataGroup = UIHelper.CreateGroup(entryGroup, "QueueEntryMetadata");
        
        CustomTextComponent entryTitle = UIHelper.CreateLabel(metadataGroup, "QueueEntryTitle", TranslationReference.Empty);
        CustomTextMeshProUGUI entryTitleTextComponent = entryTitle.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryTitleTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryTitleTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryTitle.ExtraText = TitleFormatted;
        
        CustomTextComponent entryArtist = UIHelper.CreateLabel(metadataGroup, "QueueEntryArtist", TranslationReference.Empty);
        CustomTextMeshProUGUI entryArtistTextComponent = entryArtist.Transform.GetComponent<CustomTextMeshProUGUI>();
        entryArtistTextComponent.textWrappingMode = TextWrappingModes.NoWrap;
        entryArtistTextComponent.overflowMode = TextOverflowModes.Ellipsis;
        entryArtistTextComponent.fontSize = 24;
        entryArtistTextComponent.fontStyle = FontStyles.Italic;
        entryArtist.ExtraText = $"<alpha=#AA>by <alpha=#FF>{Artist}";
        
        CustomTextComponent entryMapper = UIHelper.CreateLabel(metadataGroup, "QueueEntryMapper", TranslationReference.Empty);
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
            Object.DestroyImmediate(entryGroup.GameObject);
        });
        #endregion
    }
}