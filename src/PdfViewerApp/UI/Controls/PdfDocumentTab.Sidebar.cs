using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PdfViewerApp
{
	public partial class PdfDocumentTab
	{
		public void SetSidebarVisibility(bool isVisible)
		{
			_isSidebarVisible = isVisible;
			SidebarBorder.Visibility = ((!isVisible) ? Visibility.Collapsed : Visibility.Visible);
			SidebarColumn.Width = (isVisible ? new GridLength(260.0) : new GridLength(0.0));
			SplitterColumn.Width = (isVisible ? new GridLength(4.0) : new GridLength(0.0));
			if (isVisible && PageCount > 0)
			{
				_thumbnailLoadDeferred = false;
				RequestViewportRefresh();
			}
		}

		public void ToggleSidebar()
		{
			SetSidebarVisibility(!_isSidebarVisible);
		}

		public void GoToPage(int pageNumber)
		{
			if (PageCount <= 0)
			{
				return;
			}
			int num = Math.Clamp(pageNumber, 1, PageCount);
			SetSelectedPage(num);
			if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel))
			{
				return;
			}
			foreach (UIElement child in stackPanel.Children)
			{
				if (child is Border { Tag: var tag } border && tag is int num2 && num2 == num)
				{
					border.BringIntoView();
					break;
				}
			}
		}

		private void SetSelectedPage(int pageNumber)
		{
			if (PageCount <= 0)
			{
				SelectedPageNumber = 1;
				ReportPageChanged();
				UpdateBookmarkControlsState();
				return;
			}
			SelectedPageNumber = Math.Clamp(pageNumber, 1, PageCount);
			if (_selectedPages.Count == 0)
			{
				_selectedPages.Add(SelectedPageNumber);
				_selectionAnchorPage = SelectedPageNumber;
			}
			ReportPageChanged();
			UpdateThumbnailSelectionVisuals();
			RecordRecentPage(SelectedPageNumber);
			UpdateBookmarkControlsState();
			UnloadDistantPageContent();
		}

		private void SelectThumbnailPage(int pageNumber, ModifierKeys modifiers)
		{
			if (PageCount <= 0)
			{
				return;
			}

			int num = Math.Clamp(pageNumber, 1, PageCount);
			if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
			{
				SelectPageRange(_selectionAnchorPage, num);
			}
			else if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
			{
				if (!_selectedPages.Add(num))
				{
					_selectedPages.Remove(num);
				}
				if (_selectedPages.Count == 0)
				{
					_selectedPages.Add(num);
				}
				_selectionAnchorPage = num;
			}
			else
			{
				_selectedPages.Clear();
				_selectedPages.Add(num);
				_selectionAnchorPage = num;
			}

			SetSelectedPage(num);
		}

		private void SelectPageRange(int anchorPage, int targetPage)
		{
			if (_pageOrder.Count == 0)
			{
				_selectedPages.Clear();
				_selectedPages.Add(targetPage);
				return;
			}

			int anchorIndex = _pageOrder.IndexOf(anchorPage);
			int targetIndex = _pageOrder.IndexOf(targetPage);
			if (anchorIndex < 0 || targetIndex < 0)
			{
				_selectedPages.Clear();
				_selectedPages.Add(targetPage);
				return;
			}

			int start = Math.Min(anchorIndex, targetIndex);
			int end = Math.Max(anchorIndex, targetIndex);
			_selectedPages.Clear();
			for (int i = start; i <= end; i++)
			{
				_selectedPages.Add(_pageOrder[i]);
			}
		}

		private List<int> GetSelectedPagesInOrder()
		{
			if (_selectedPages.Count == 0)
			{
				return new List<int>();
			}

			List<int> ordered = new List<int>();
			foreach (int page in _pageOrder)
			{
				if (_selectedPages.Contains(page))
				{
					ordered.Add(page);
				}
			}
			return ordered;
		}

		private void EnsureContextMenuSelection(int pageNumber)
		{
			if (PageCount <= 0)
			{
				return;
			}

			int num = Math.Clamp(pageNumber, 1, PageCount);
			if (!_selectedPages.Contains(num))
			{
				_selectedPages.Clear();
				_selectedPages.Add(num);
				_selectionAnchorPage = num;
			}

			SetSelectedPage(num);
		}

		private System.Windows.Controls.MenuItem CreateThumbnailMenuItem(string header, RoutedEventHandler clickHandler)
		{
			System.Windows.Controls.MenuItem item = new System.Windows.Controls.MenuItem
			{
				Header = header
			};
			item.Click += clickHandler;
			return item;
		}

		private void ClearThumbnailSelection()
		{
			if (PageCount <= 0)
			{
				return;
			}

			int num = Math.Clamp(SelectedPageNumber, 1, PageCount);
			_selectedPages.Clear();
			_selectedPages.Add(num);
			_selectionAnchorPage = num;
			UpdateThumbnailSelectionVisuals();
			LogStatus($"Selection cleared to page {num}.");
		}

		private void SelectAllThumbnailPages()
		{
			if (PageCount <= 0)
			{
				return;
			}

			EnsurePageOrderInitialized();
			ApplyThumbnailSelection(_pageOrder, $"Selected all {PageCount} pages.");
		}

		private void InvertThumbnailSelection()
		{
			if (PageCount <= 0)
			{
				return;
			}

			EnsurePageOrderInitialized();
			HashSet<int> currentSelection = new HashSet<int>(_selectedPages);
			List<int> inverted = _pageOrder.Where(page => !currentSelection.Contains(page)).ToList();
			if (inverted.Count == 0)
			{
				inverted.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
			}

			ApplyThumbnailSelection(inverted, $"Inverted selection: {inverted.Count} pages selected.");
		}

		private void SelectThumbnailPagesByParity(bool selectOddPages)
		{
			if (PageCount <= 0)
			{
				return;
			}

			EnsurePageOrderInitialized();
			List<int> pages = _pageOrder.Where(page => selectOddPages ? page % 2 == 1 : page % 2 == 0).ToList();
			if (pages.Count == 0)
			{
				LogStatus(selectOddPages ? "No odd pages to select." : "No even pages to select.");
				return;
			}

			ApplyThumbnailSelection(pages, selectOddPages ? $"Selected {pages.Count} odd pages." : $"Selected {pages.Count} even pages.");
		}

		private void ApplyThumbnailSelection(IEnumerable<int> pages, string statusMessage)
		{
			List<int> pageList = pages.Where(page => page >= 1 && page <= PageCount).Distinct().ToList();
			if (pageList.Count == 0)
			{
				return;
			}

			_selectedPages.Clear();
			foreach (int page in pageList)
			{
				_selectedPages.Add(page);
			}

			_selectionAnchorPage = pageList[0];
			SetSelectedPage(pageList[0]);
			LogStatus(statusMessage);
		}

		private void RecordRecentPage(int pageNumber)
		{
			if (PageCount <= 0)
			{
				return;
			}

			int num = Math.Clamp(pageNumber, 1, PageCount);
			_recentPages.Remove(num);
			_recentPages.Insert(0, num);
			while (_recentPages.Count > RecentPagesLimit)
			{
				_recentPages.RemoveAt(_recentPages.Count - 1);
			}

			RefreshRecentPagesPanel();
			SaveNavigationState();
		}

		private void LoadPersistedNavigationState(string path)
		{
			DocumentNavigationState state = DocumentNavigationStateService.Load(path, PageCount);
			_recentPages.Clear();
			_recentPages.AddRange(state.RecentPages);
			_bookmarkedPages.Clear();
			foreach (int page in state.BookmarkedPages)
			{
				_bookmarkedPages.Add(page);
			}

			RefreshRecentPagesPanel();
			RefreshBookmarksPanel();
			UpdateBookmarkControlsState();
		}

		private void SaveNavigationState()
		{
			if (string.IsNullOrWhiteSpace(CurrentPdfPath) || PageCount <= 0)
			{
				return;
			}

			DocumentNavigationStateService.Save(CurrentPdfPath, _recentPages, _bookmarkedPages, PageCount);
		}

		private void ToggleBookmarkCurrentPage()
		{
			if (PageCount <= 0)
			{
				return;
			}

			int num = Math.Clamp(SelectedPageNumber, 1, PageCount);
			if (!_bookmarkedPages.Add(num))
			{
				_bookmarkedPages.Remove(num);
			}

			RefreshBookmarksPanel();
			SaveNavigationState();
		}

		private void BookmarkSelectedPages()
		{
			if (PageCount <= 0)
			{
				return;
			}

			List<int> pages = GetSelectedPagesInOrder();
			if (pages.Count == 0)
			{
				pages.Add(Math.Clamp(SelectedPageNumber, 1, PageCount));
			}

			foreach (int page in pages)
			{
				_bookmarkedPages.Add(page);
			}

			RefreshBookmarksPanel();
			SaveNavigationState();
			LogStatus(pages.Count == 1 ? $"Bookmarked page {pages[0]}." : $"Bookmarked {pages.Count} selected pages.");
		}

		private void BookmarkCurrentPage_Click(object sender, RoutedEventArgs e)
		{
			ToggleBookmarkCurrentPage();
		}

		private void ClearBookmarks_Click(object sender, RoutedEventArgs e)
		{
			_bookmarkedPages.Clear();
			RefreshBookmarksPanel();
			SaveNavigationState();
		}

		private void RefreshRecentPagesPanel()
		{
			RecentPagesContainer.Children.Clear();
			if (PageCount <= 0 || _recentPages.Count == 0)
			{
				RecentPagesContainer.Children.Add(CreateEmptyPanelMessage("No recent pages"));
				return;
			}

			foreach (int recentPage in _recentPages)
			{
				RecentPagesContainer.Children.Add(CreatePageJumpButton($"Page {recentPage}", recentPage, recentPage == SelectedPageNumber));
			}
		}

		private void RefreshBookmarksPanel()
		{
			BookmarksContainer.Children.Clear();
			if (PageCount <= 0 || _bookmarkedPages.Count == 0)
			{
				BookmarksContainer.Children.Add(CreateEmptyPanelMessage("No bookmarks"));
				UpdateBookmarkControlsState();
				return;
			}

			foreach (int bookmarkedPage in _bookmarkedPages.OrderBy(page => page))
			{
				BookmarksContainer.Children.Add(CreateBookmarkRow(bookmarkedPage, bookmarkedPage == SelectedPageNumber));
			}

			UpdateBookmarkControlsState();
		}

		private void UpdateBookmarkControlsState()
		{
			if (BookmarkCurrentPageBtn != null)
			{
				BookmarkCurrentPageBtn.Content = (_bookmarkedPages.Contains(SelectedPageNumber) ? "Remove bookmark" : "Bookmark");
			}

			if (ClearBookmarksBtn != null)
			{
				ClearBookmarksBtn.IsEnabled = _bookmarkedPages.Count > 0;
			}
		}

		private UIElement CreateEmptyPanelMessage(string message)
		{
			return new TextBlock
			{
				Text = message,
				Foreground = Brushes.SlateGray,
				FontSize = 11.0,
				Margin = new Thickness(4.0, 0.0, 4.0, 8.0),
				TextWrapping = TextWrapping.Wrap
			};
		}

		private System.Windows.Controls.Button CreatePageJumpButton(string label, int pageNumber, bool selected)
		{
			System.Windows.Controls.Button button = new System.Windows.Controls.Button
			{
				Content = label,
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0),
				Padding = new Thickness(8.0, 5.0, 8.0, 5.0),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Left,
				Background = (selected ? new SolidColorBrush(Color.FromRgb(15, 118, 110)) : new SolidColorBrush(Color.FromRgb(30, 41, 59))),
				Foreground = Brushes.White,
				BorderBrush = (selected ? new SolidColorBrush(Color.FromRgb(45, 212, 191)) : new SolidColorBrush(Color.FromRgb(51, 65, 85))),
				BorderThickness = new Thickness(1.0),
				ToolTip = $"Go to page {pageNumber}"
			};
			button.Click += delegate
			{
				GoToPage(pageNumber);
			};
			return button;
		}

		private UIElement CreateBookmarkRow(int pageNumber, bool selected)
		{
			DockPanel row = new DockPanel
			{
				LastChildFill = true,
				Margin = new Thickness(0.0, 0.0, 0.0, 4.0)
			};

			System.Windows.Controls.Button removeButton = new System.Windows.Controls.Button
			{
				Content = "X",
				Width = 28.0,
				Padding = new Thickness(0.0),
				Margin = new Thickness(6.0, 0.0, 0.0, 0.0),
				Background = new SolidColorBrush(Color.FromRgb(69, 26, 26)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromRgb(127, 29, 29)),
				BorderThickness = new Thickness(1.0),
				ToolTip = $"Remove bookmark on page {pageNumber}"
			};
			removeButton.Click += delegate
			{
				_bookmarkedPages.Remove(pageNumber);
				RefreshBookmarksPanel();
				SaveNavigationState();
			};
			DockPanel.SetDock(removeButton, Dock.Right);
			row.Children.Add(removeButton);
			row.Children.Add(CreatePageJumpButton($"Page {pageNumber}", pageNumber, selected));
			return row;
		}

		private void UpdateThumbnailSelectionVisuals()
		{
			foreach (object child in ThumbnailContainer.Children)
			{
				if (child is Border { Tag: var tag } border && tag is int num)
				{
					bool isActive = num == SelectedPageNumber;
					bool isSelected = _selectedPages.Contains(num);
					border.BorderBrush = (isActive ? Brushes.DeepSkyBlue : isSelected ? new SolidColorBrush(Color.FromRgb(20, 184, 166)) : Brushes.Gray);
					border.BorderThickness = (isActive ? new Thickness(3.0) : isSelected ? new Thickness(2.0) : new Thickness(1.0));
					border.Background = (isSelected ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : Brushes.White);
				}
			}
		}

		private Border? GetPageBorder(int pageNumber)
		{
			if (PagesHost.Children.Count == 0 || !(PagesHost.Children[0] is StackPanel stackPanel))
			{
				return null;
			}
			foreach (UIElement child in stackPanel.Children)
			{
				if (child is Border { Tag: var tag } border && tag is int num && num == pageNumber)
				{
					return border;
				}
			}
			return null;
		}

		private Border? GetThumbnailBorder(int pageNumber)
		{
			foreach (UIElement child in ThumbnailContainer.Children)
			{
				if (child is Border { Tag: var tag } border && tag is int num && num == pageNumber)
				{
					return border;
				}
			}
			return null;
		}

		private static IEnumerable<int> GetProgressivePageOrder(int selectedPage, int pageCount)
		{
			selectedPage = Math.Clamp(selectedPage, 1, Math.Max(1, pageCount));
			yield return selectedPage;
			for (int distance = 1; distance < pageCount; distance++)
			{
				int num = selectedPage - distance;
				int next = selectedPage + distance;
				if (num >= 1)
				{
					yield return num;
				}
				if (next <= pageCount)
				{
					yield return next;
				}
			}
		}

		private void EnsurePageOrderInitialized()
		{
			if (_pageOrder.Count == PageCount)
			{
				return;
			}

			_pageOrder.Clear();
			_pageOrder.AddRange(Enumerable.Range(1, PageCount));
		}

		private bool MoveSelectedPagesUp(HashSet<int> selectedSet)
		{
			bool moved = false;
			for (int i = 1; i < _pageOrder.Count; i++)
			{
				if (selectedSet.Contains(_pageOrder[i]) && !selectedSet.Contains(_pageOrder[i - 1]))
				{
					(_pageOrder[i - 1], _pageOrder[i]) = (_pageOrder[i], _pageOrder[i - 1]);
					moved = true;
				}
			}

			return moved;
		}

		private bool MoveSelectedPagesDown(HashSet<int> selectedSet)
		{
			bool moved = false;
			for (int i = _pageOrder.Count - 2; i >= 0; i--)
			{
				if (selectedSet.Contains(_pageOrder[i]) && !selectedSet.Contains(_pageOrder[i + 1]))
				{
					(_pageOrder[i + 1], _pageOrder[i]) = (_pageOrder[i], _pageOrder[i + 1]);
					moved = true;
				}
			}

			return moved;
		}

		private System.Windows.Controls.ContextMenu CreateThumbnailContextMenu(int pageNumber)
		{
			System.Windows.Controls.ContextMenu contextMenu = new System.Windows.Controls.ContextMenu();
			contextMenu.Opened += delegate
			{
				EnsureContextMenuSelection(pageNumber);
			};

			contextMenu.Items.Add(CreateThumbnailMenuItem("Rotate left", async delegate
			{
				await RotateSelectedPageAsync(-90);
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Rotate right", async delegate
			{
				await RotateSelectedPageAsync(90);
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Extract selected", async delegate
			{
				await ExtractSelectedPagesAsync();
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Duplicate selected", async delegate
			{
				await DuplicateSelectedPageAsync();
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Delete selected", async delegate
			{
				await DeleteSelectedPageAsync();
			}));
			contextMenu.Items.Add(new System.Windows.Controls.Separator());
			contextMenu.Items.Add(CreateThumbnailMenuItem("Move selected up", delegate
			{
				MoveSelectedPage(-1);
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Move selected down", delegate
			{
				MoveSelectedPage(1);
			}));
			contextMenu.Items.Add(new System.Windows.Controls.Separator());
			contextMenu.Items.Add(CreateThumbnailMenuItem("Select all pages", delegate
			{
				SelectAllThumbnailPages();
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Invert selection", delegate
			{
				InvertThumbnailSelection();
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Select odd pages", delegate
			{
				SelectThumbnailPagesByParity(selectOddPages: true);
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Select even pages", delegate
			{
				SelectThumbnailPagesByParity(selectOddPages: false);
			}));
			contextMenu.Items.Add(new System.Windows.Controls.Separator());
			contextMenu.Items.Add(CreateThumbnailMenuItem("Bookmark selected", delegate
			{
				BookmarkSelectedPages();
			}));
			contextMenu.Items.Add(CreateThumbnailMenuItem("Clear selection", delegate
			{
				ClearThumbnailSelection();
			}));

			return contextMenu;
		}
	}
}
