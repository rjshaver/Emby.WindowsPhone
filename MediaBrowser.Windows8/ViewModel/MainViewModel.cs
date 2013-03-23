using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Ioc;
using GalaSoft.MvvmLight.Messaging;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Windows8.Model;
using MediaBrowser.Windows8.Views;
using ReflectionIT.Windows8.Helpers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MediaBrowser.Windows8.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly NavigationService navigationService;
        private readonly ExtendedApiClient ApiClient;
        private bool dataLoaded;

        private const int RecentItems = 2;
        private const int Collections = 1;
        private const int Favourites = 3;
        private const int Resumable = 0;

        private BaseItemDto[] recentItems;

        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel(NavigationService navigation, ExtendedApiClient apiClient)
        {
            navigationService = navigation;
            ApiClient = apiClient;
            Reset();

            if (IsInDesignMode)
            {
                // Code runs in Blend --> create design time data.
                Groups[Collections].Items.Add(new BaseItemDto{ Name = "Movies"}.ToWrapper());
                Groups[RecentItems].Items.Add(new BaseItemDto
                                        {
                                            Id = "c0ac2259ea0b6d18b0a9c9c3c2a8da68",
                                            Name = "Jurassic Park",
                                            DateCreated = DateTime.Now,
                                            Type= "Movie"
                                        }.ToWrapper(2,2));
                Groups[RecentItems].Items.Add(new BaseItemDto
                                                  {
                                                      Id = "c0ac2259ea0b6d18b0a9c9c3c2a8da68",
                                                      Name = "Jurassic Park 3",
                                                      Type = "Movie"
                                                  }.ToWrapper());
            }
            else
            {
                // Code runs "for real"
                WireCommands();
                WireMessages();
                ProgressVisibility = Visibility.Collapsed;
                PlayResumeVisibility = Visibility.Collapsed;
                PinCollectionVisibility = Visibility.Collapsed;
            }
        }

        private void WireMessages()
        {
            Messenger.Default.Register<NotificationMessage>(this, async m =>
            {
                if (m.Notification.Equals(Constants.MainPageLoadedMsg))
                {
                    SelectedItem = null;
                    if (navigationService.IsNetworkAvailable && !dataLoaded)
                    {
                        ProgressVisibility = Visibility.Visible;

                        ProgressText = "Getting resumable items...";
                        bool resumable = await GetResumableItems();

                        ProgressText = "Getting collections...";
                        bool collections = await GetCollections();

                        ProgressText = "Getting recent items...";
                        bool recent = await GetRecentlyAddedItems();

                        ProgressText = "Getting favourites...";
                        bool faves = await GetFavouriteItems();
                        
                        dataLoaded = (collections && recent && resumable);
                        if (!dataLoaded)
                        {
                            await
                                MessageBox.ShowAsync(
                                    "There was an error getting some of the data. Please try again later.", "Error",
                                    MessageBoxButton.OK);
                        }
                        SelectedItem = null;
                        ProgressVisibility = Visibility.Collapsed;
                    }
                }
                if(m.Notification.Equals(Constants.ClearEverythingMsg))
                {
                    Reset();
                }
            });

            Messenger.Default.Register<PropertyChangedMessage<object>>(this, async m =>
                                                                                 {
                                                                                     if (m.PropertyName.Equals("IncludeTrailersInRecentItems"))
                                                                                     {
                                                                                         await SortRecentItems(recentItems);
                                                                                     }
                                                                                 });
        }
        
        private void Reset()
        {
            Groups = new List<Group<GridItemWrapper<BaseItemDto>>>();
            for (var i = 0; i < 4; i++) Groups.Add(new Group<GridItemWrapper<BaseItemDto>>());

            Groups[Collections] = new Group<GridItemWrapper<BaseItemDto>> { Title = "Collections" };
            Groups[RecentItems] = new Group<GridItemWrapper<BaseItemDto>> { Title = "Recently Added" };
            Groups[Resumable] = new Group<GridItemWrapper<BaseItemDto>> { Title = "Resumable" };
            Groups[Favourites] = new Group<GridItemWrapper<BaseItemDto>> { Title = "Favourites" };

            dataLoaded = false;
        }

        private void OnSelectedItemChanged()
        {
            IsOpen = SelectedItem != null;
            IsSticky = IsOpen;
            if (IsSticky)
            {
                switch (SelectedItem.Item.Type.ToLower())
                {
                    case "movie":
                    case "episode":
                        // Show play/resume button
                        PlayResumeVisibility = Visibility.Visible;
                        PinCollectionVisibility = Visibility.Collapsed;
                        break;
                    case "folder":
                    case "collectionfolder":
                        // Show pin to start button
                        PlayResumeVisibility = Visibility.Collapsed;
                        PinCollectionVisibility = Visibility.Visible;
                        break;
                    default:
                        PlayResumeVisibility = Visibility.Collapsed;
                        PinCollectionVisibility = Visibility.Collapsed;
                        break;
                }
            }
        }

        private void WireCommands()
        {
            BackButtonPressed = new RelayCommand(async () =>
            {
            });

            PlayTrailerCommand = new RelayCommand<BaseItemDto>(item =>
            {
                Messenger.Default.Send(new NotificationMessage(item, Constants.PlayTrailerMsg));
                navigationService.Navigate<VideoPlayer>();
            });

            PinItemCommand = new RelayCommand<BaseItemDto>(async item =>
                                                               {
                                                                   if (item == null) return;


                                                               });

            ItemClicked = new RelayCommand<ItemClickEventArgs>(args =>
                                                                   {
                                                                       var dtoItem = (GridItemWrapper<BaseItemDto>) args.ClickedItem;
                                                                       navigationService.NavigateToPage(dtoItem.Item);
                                                                   });
            NavigateToPage = new RelayCommand<object>(navigationService.NavigateToPage);
            PlayVideoCommand = new RelayCommand<BaseItemDto>(item => navigationService.PlayVideoItem(item, false));
            ResumeVideoCommand = new RelayCommand<BaseItemDto>(item => navigationService.PlayVideoItem(item, true));
            GoHome = new RelayCommand(() => navigationService.Navigate<MainPage>());
        }

        public async Task<bool> GetCollections()
        {
            try
            {
                var query = new ItemQuery
                                {
                                    UserId = App.Settings.LoggedInUser.Id
                                };
                var items = await ApiClient.GetItemsAsync(query);
                if (items != null && items.Items.Any())
                {
                    foreach (var item in items.Items)
                    {
                        Groups[Collections].Items.Add(item.ToWrapper());
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> GetFavouriteItems()
        {
            try
            {
                var query = new ItemQuery
                                {
                                    UserId = App.Settings.LoggedInUser.Id,
                                    Filters = new[] {ItemFilter.IsFavorite,},
                                    Recursive=true
                                };
                var items = await ApiClient.GetItemsAsync(query);
                if (items != null && items.Items != null)
                {
                    foreach (var item in items.Items)
                    {
                        Groups[Favourites].Items.Add(item.ToWrapper());
                    }
                    if (Groups[Favourites].Items.Any())
                    {
                        var firstItem = Groups[Favourites].Items.First();
                        firstItem.ColSpan = firstItem.RowSpan = 2;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> GetResumableItems()
        {
            try
            {
                var query = new ItemQuery
                                {
                                    UserId = App.Settings.LoggedInUser.Id,
                                    Filters = new[] {ItemFilter.IsResumable,},
                                    Recursive = true
                                };
                var items = await ApiClient.GetItemsAsync(query);
                if (items != null && items.Items != null)
                {
                    foreach (var item in items.Items)
                    {
                        Groups[Resumable].Items.Add(item.ToWrapper());
                    }
                    if (Groups[Resumable].Items.Any())
                    {
                        var firstItem = Groups[Resumable].Items.First();
                        firstItem.ColSpan = firstItem.RowSpan = 2;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }

        }

        public async Task<bool> GetRecentlyAddedItems()
        {
            try
            {
                var query = new ItemQuery
                                {
                                    Filters = new[] {ItemFilter.IsRecentlyAdded, ItemFilter.IsNotFolder, },
                                    UserId = App.Settings.LoggedInUser.Id,
                                    Fields = new[]
                                                 {
                                                     ItemFields.SeriesInfo,
                                                     ItemFields.DateCreated,
                                                     ItemFields.UserData, 
                                                 },
                                    Recursive = true
                                };
                var items = await ApiClient.GetItemsAsync(query);
                recentItems = items.Items;
                await SortRecentItems(items.Items);
                
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task SortRecentItems(BaseItemDto[] dtoBaseItem)
        {
            if (dtoBaseItem != null && dtoBaseItem.Any())
            {
                Groups[RecentItems].Items.Clear();
                var recent = new List<BaseItemDto>();
                
                await TaskEx.Run(() =>
                               {
                                   var episodesBySeries = dtoBaseItem
                                       .Where(x => x.Type == "Episode")
                                       .GroupBy(l => l.SeriesId)
                                       .Select(g => new
                                                        {
                                                            Id = g.Key,
                                                            Name = g.Select(l => l.SeriesName).FirstOrDefault(),
                                                            Count = g.Count(),
                                                            CreatedDate = g.OrderByDescending(l => l.DateCreated).First().DateCreated
                                                        }).ToList();
                                   var seriesList = new List<BaseItemDto>();
                                   if (episodesBySeries != null && episodesBySeries.Any())
                                   {
                                       seriesList.AddRange(episodesBySeries.Select(series => new BaseItemDto
                                                                                                 {
                                                                                                     Name = string.Format("{0} ({1} items)", series.Name, series.Count),
                                                                                                     Id = series.Id,
                                                                                                     DateCreated = series.CreatedDate,
                                                                                                     Type = "Series",
                                                                                                     SortName = Constants.GetTvInformationMsg,
                                                                                                     ImageTags = new Dictionary<ImageType, Guid>{{ImageType.Primary, Guid.NewGuid()}}
                                                                                                 }));
                                   }

                                   recent = dtoBaseItem
                                       .Where(x => x.Type != "Episode")
                                       .Union(seriesList)
                                       .OrderByDescending(x => x.DateCreated).ToList();
                                   if (!SimpleIoc.Default.GetInstance<SpecificSettings>().IncludeTrailersInRecentItems)
                                       recent = recent.Where(y => y.Type != "Trailer").ToList();
                                   
                               });
                foreach (var item in recent)
                {
                    Groups[RecentItems].Items.Add(item.ToWrapper());
                }
                var firstItem = Groups[RecentItems].Items.First();
                firstItem.ColSpan = firstItem.RowSpan = 2;
            }
        }

        public RelayCommand BackButtonPressed { get; set; }
        public RelayCommand<object> NavigateToPage { get; set; }
        public RelayCommand GoHome { get; set; }
        public RelayCommand<BaseItemDto> PlayVideoCommand { get; set; }
        public RelayCommand<BaseItemDto> ResumeVideoCommand { get; set; }
        public RelayCommand<BaseItemDto> PlayTrailerCommand { get; set; }
        public RelayCommand<ItemClickEventArgs> ItemClicked { get; set; }
        public RelayCommand<BaseItemDto> PinItemCommand { get; set; }
        
        public string ProgressText { get; set; }
        public Visibility ProgressVisibility { get; set; }
        public bool IsSticky { get; set; }
        public bool IsOpen { get; set; }
       
        public List<Group<GridItemWrapper<BaseItemDto>>> Groups { get; set; }
        public GridItemWrapper<BaseItemDto> SelectedItem { get; set; }

        public Visibility PlayResumeVisibility { get; set; }
        public Visibility PinCollectionVisibility { get; set; }
        
    }
}