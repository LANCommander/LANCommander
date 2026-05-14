using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.Services;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ChatWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatWindowViewModel> _logger;
    private IChatClient? _chatClient;
    private AuthenticationService? _authenticationService;
    private NotificationService? _notificationService;
    private bool _initialized;

    // ── Thread list ──────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThreads))]
    private ObservableCollection<ChatThreadViewModel> _threads = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedThread))]
    private ChatThreadViewModel? _selectedThread;

    [ObservableProperty]
    private string _messageInput = string.Empty;

    [ObservableProperty]
    private int _totalUnreadCount;

    [ObservableProperty]
    private bool _isLoading;

    public bool HasSelectedThread => SelectedThread != null;

    public bool HasThreads => Threads.Count > 0;

    // ── New thread creation ──────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedUsers))]
    private ObservableCollection<UserSelectionViewModel> _availableUsers = new();

    [ObservableProperty]
    private ObservableCollection<UserSelectionViewModel> _filteredUsers = new();

    [ObservableProperty]
    private string _userSearchText = string.Empty;

    [ObservableProperty]
    private bool _isCreatingThread;

    [ObservableProperty]
    private bool _isStartingThread;

    public bool HasSelectedUsers => AvailableUsers.Any(u => u.IsSelected);

    // ── Window state ─────────────────────────────────────────────────────────

    /// <summary>Set by the window code-behind when it gains/loses activation.</summary>
    public bool IsWindowActive { get; set; }

    /// <summary>Fired when a message arrives while the chat window is inactive.</summary>
    public event EventHandler<(string threadTitle, string senderName)>? MessageReceivedWhileInactive;

    public ChatWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ChatWindowViewModel>>();
    }

    public async Task InitializeAsync()
    {
        try
        {
            _chatClient = _serviceProvider.GetRequiredService<IChatClient>();
            _authenticationService = _serviceProvider.GetRequiredService<AuthenticationService>();
            _notificationService = _serviceProvider.GetService<NotificationService>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve ChatClient");
        }
    }

    public async Task LoadThreadsAsync(bool force = false)
    {
        if (_chatClient == null || (IsLoading && !force))
            return;

        if (_initialized && !force)
            return;

        IsLoading = true;

        try
        {
            var threads = force
                ? await _chatClient.LoadThreadsAsync()
                : await _chatClient.GetThreadsAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Threads.Clear();

                foreach (var thread in threads.OrderByDescending(t => t.LastActivityOn))
                    Threads.Add(WrapThread(thread));

                OnPropertyChanged(nameof(HasThreads));

                if (SelectedThread == null && Threads.Count > 0)
                    SelectedThread = Threads[0];
            });

            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat threads");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Guid GetCurrentUserId() => _authenticationService?.GetUserId() ?? Guid.Empty;

    private ChatThreadViewModel WrapThread(ChatThread thread)
    {
        var vm = new ChatThreadViewModel(thread, GetCurrentUserId());

        thread.DispatcherInvoke = async action => await Dispatcher.UIThread.InvokeAsync(action);

        thread.OnMessageReceivedAsync = async message =>
        {
            var isActiveThread = SelectedThread?.Thread.Id == thread.Id;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsWindowActive || !isActiveThread)
                {
                    vm.UnreadCount++;
                    UpdateTotalUnreadCount();
                }

                MoveThreadToTop(vm);
            });

            if (!IsWindowActive)
            {
                MessageReceivedWhileInactive?.Invoke(this, (thread.Title, message.UserName));

                _notificationService?.NotifyChatMessage(
                    thread.Title,
                    message.UserName,
                    message.Content);
            }
        };

        return vm;
    }

    partial void OnSelectedThreadChanged(ChatThreadViewModel? oldValue, ChatThreadViewModel? newValue)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
        
        if (newValue != null)
        {
            _ = MarkThreadReadAsync(newValue);
            _ = LoadThreadMessagesAsync(newValue);
        }
    }

    private async Task LoadThreadMessagesAsync(ChatThreadViewModel threadVm)
    {
        if (_chatClient == null)
            return;

        // Skip if the thread already has messages loaded (from this session)
        if (threadVm.Thread.MessageGroups.Count > 0)
            return;

        try
        {
            var response = await _chatClient.GetMessagesAsync(threadVm.Thread.Id, null, 50);

            if (response.Items != null && response.Items.Any())
                await threadVm.Thread.MessagesReceivedAsync(response.Items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load messages for thread {ThreadId}", threadVm.Thread.Id);
        }
    }

    private async Task MarkThreadReadAsync(ChatThreadViewModel threadVm)
    {
        if (_chatClient == null)
            return;

        try
        {
            threadVm.UnreadCount = 0;
            
            UpdateTotalUnreadCount();
            
            await _chatClient.UpdatedReadStatus(threadVm.Thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update read status for thread {ThreadId}", threadVm.Thread.Id);
        }
    }

    private void UpdateTotalUnreadCount() =>
        TotalUnreadCount = Threads.Sum(t => t.UnreadCount);

    private void MoveThreadToTop(ChatThreadViewModel threadVm)
    {
        var index = Threads.IndexOf(threadVm);
        
        if (index > 0)
            Threads.Move(index, 0);
    }

    // ── Send message ─────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSendMessage))]
    private async Task SendMessageAsync()
    {
        if (_chatClient == null || SelectedThread == null || string.IsNullOrWhiteSpace(MessageInput))
            return;

        var text = MessageInput.Trim();
        
        MessageInput = string.Empty;

        try
        {
            await _chatClient.SendMessageAsync(SelectedThread.Thread.Id, text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send chat message");
            
            MessageInput = text;
        }
    }

    private bool CanSendMessage() =>
        SelectedThread != null && !string.IsNullOrWhiteSpace(MessageInput);

    partial void OnMessageInputChanged(string value) =>
        SendMessageCommand.NotifyCanExecuteChanged();

    // ── New thread ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task OpenNewThreadAsync()
    {
        if (_chatClient == null)
            return;

        IsCreatingThread = true;
        UserSearchText = string.Empty;

        try
        {
            var users = await _chatClient.GetUsersAsync();
            var currentUserId = _authenticationService?.GetUserId() ?? Guid.Empty;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableUsers.Clear();

                foreach (var user in users.Where(u => u.Id != currentUserId))
                {
                    var vm = new UserSelectionViewModel(user);
                    
                    vm.PropertyChanged += (_, e) =>
                    {
                        if (e.PropertyName == nameof(UserSelectionViewModel.IsSelected))
                            OnPropertyChanged(nameof(HasSelectedUsers));
                    };
                    
                    AvailableUsers.Add(vm);
                }

                ApplyUserFilter();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load users for new thread");
            IsCreatingThread = false;
        }
    }

    [RelayCommand]
    private void CancelNewThread()
    {
        IsCreatingThread = false;
        UserSearchText = string.Empty;
        
        foreach (var u in AvailableUsers)
            u.IsSelected = false;
    }

    [RelayCommand]
    private async Task StartNewThreadAsync()
    {
        if (_chatClient == null)
            return;

        var selected = AvailableUsers.Where(u => u.IsSelected).ToList();
        
        if (selected.Count == 0)
            return;

        IsStartingThread = true;

        try
        {
            var currentUserId = GetCurrentUserId();
            var userIds = selected.Select(u => u.User.Id.ToString()).ToList();

            if (currentUserId != Guid.Empty && !userIds.Contains(currentUserId.ToString()))
                userIds.Add(currentUserId.ToString());

            var threadId = await _chatClient.StartThreadAsync(userIds);

            if (threadId != Guid.Empty)
            {
                // Reload threads so the new one appears
                await LoadThreadsAsync(force: true);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Select the newly created thread
                    var newThread = Threads.FirstOrDefault(t => t.Thread.Id == threadId);
                    
                    if (newThread != null)
                        SelectedThread = newThread;

                    IsCreatingThread = false;
                    UserSearchText = string.Empty;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start new chat thread");
        }
        finally
        {
            IsStartingThread = false;
        }
    }

    partial void OnUserSearchTextChanged(string value) => ApplyUserFilter();

    private void ApplyUserFilter()
    {
        FilteredUsers.Clear();
        var query = UserSearchText?.Trim() ?? string.Empty;

        foreach (var user in AvailableUsers)
        {
            if (string.IsNullOrEmpty(query) || user.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                FilteredUsers.Add(user);
        }
    }
}

// ── ChatThreadViewModel ───────────────────────────────────────────────────────

public partial class ChatThreadViewModel : ViewModelBase
{
    public ChatThread Thread { get; }

    public string Title => Thread.Title;

    public ObservableCollection<ChatMessageGroup> MessageGroups => Thread.MessageGroups;

    public List<User> OtherParticipants { get; }

    public string OtherParticipantNames => string.Join(", ", OtherParticipants.Select(p => p.Name));

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    private int _unreadCount;

    public bool HasUnread => UnreadCount > 0;

    public ChatThreadViewModel(ChatThread thread, Guid currentUserId)
    {
        Thread = thread;
        OtherParticipants = thread.Participants
            .Where(p => p.Id != currentUserId)
            .ToList();
    }
}

// ── UserSelectionViewModel ────────────────────────────────────────────────────

public partial class UserSelectionViewModel : ViewModelBase
{
    public User User { get; }

    public string Name => User.Name;

    [ObservableProperty]
    private bool _isSelected;

    public UserSelectionViewModel(User user)
    {
        User = user;
    }
}
