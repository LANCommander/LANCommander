using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels;

public partial class ChatWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatWindowViewModel> _logger;
    private IChatClient? _chatClient;
    private AuthenticationService? _authenticationService;
    private NotificationService? _notificationService;
    private ChatAvatarCache? _avatarCache;
    private ChatTypingNotifier? _typingNotifier;
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

    /// <summary>Fired when someone else's message arrives while the chat window is inactive.</summary>
    public event EventHandler<(string threadTitle, string senderName)>? MessageReceivedWhileInactive;

    /// <summary>Asks for the chat window to be shown, e.g. from a clicked notification.</summary>
    public event EventHandler? OpenRequested;

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
            _avatarCache = _serviceProvider.GetService<ChatAvatarCache>();

            var chatClient = _chatClient;
            _typingNotifier = new ChatTypingNotifier((threadId, typing) => chatClient.SendTypingAsync(threadId, typing));

            _chatClient.ThreadAdded += OnThreadAdded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve ChatClient");
        }
    }

    /// <summary>
    /// Opens the most recently active conversation if none is open yet. Done when the window opens
    /// rather than when threads load, since opening one marks it read.
    /// </summary>
    public void SelectMostRecentThread()
    {
        if (SelectedThread == null && Threads.Count > 0)
            SelectedThread = Threads[0];
    }

    /// <summary>Someone started a thread with this user; listen to it like the rest</summary>
    private void OnThreadAdded(ChatThread thread) => Dispatcher.UIThread.Post(() =>
    {
        if (Threads.Any(t => t.Thread.Id == thread.Id))
            return;

        Threads.Insert(0, WrapThread(thread));

        OnPropertyChanged(nameof(HasThreads));
    });

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
            });

            _initialized = true;

            await LoadUnreadCountsAsync();
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

    /// <summary>What arrived while the launcher was closed, so the badges start out right</summary>
    private async Task LoadUnreadCountsAsync()
    {
        if (_chatClient == null)
            return;

        foreach (var threadVm in Threads.ToList())
        {
            // The open thread is being read
            if (threadVm == SelectedThread)
                continue;

            try
            {
                var count = await _chatClient.GetUnreadMessageCountAsync(threadVm.Thread.Id);

                await Dispatcher.UIThread.InvokeAsync(() => threadVm.UnreadCount = count);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not count the unread messages in thread {ThreadId}", threadVm.Thread.Id);
            }
        }

        await Dispatcher.UIThread.InvokeAsync(UpdateTotalUnreadCount);
    }

    private Guid GetCurrentUserId() => _authenticationService?.GetUserId() ?? Guid.Empty;

    private ChatThreadViewModel WrapThread(ChatThread thread)
    {
        var vm = new ChatThreadViewModel(thread, GetCurrentUserId());

        thread.DispatcherInvoke = async action => await Dispatcher.UIThread.InvokeAsync(action);

        thread.OnStartTypingAsync = userId =>
        {
            // This user's own typing elsewhere is nothing to show them
            if (Guid.TryParse(userId, out var id) && id != GetCurrentUserId())
                vm.Typing.Start(id);

            return Task.CompletedTask;
        };

        thread.OnStopTypingAsync = userId =>
        {
            if (Guid.TryParse(userId, out var id))
                vm.Typing.Stop(id);

            return Task.CompletedTask;
        };

        thread.OnMessageReceivedAsync = async message =>
        {
            // The server echoes this user's own messages back to them, from here or elsewhere
            var isOwnMessage = message.UserId == GetCurrentUserId();
            var isWindowActive = false;

            // Their message is what they were typing
            vm.Typing.Stop(message.UserId);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                isWindowActive = IsWindowActive;

                var isBeingRead = isWindowActive && SelectedThread?.Thread.Id == thread.Id;

                if (isBeingRead)
                    _ = MarkThreadReadAsync(vm);
                else if (!isOwnMessage)
                {
                    vm.UnreadCount++;
                    UpdateTotalUnreadCount();
                }

                MoveThreadToTop(vm);
            });

            if (isOwnMessage || isWindowActive)
                return;

            MessageReceivedWhileInactive?.Invoke(this, (thread.Title, message.UserName));

            // Avatars are keyed by username; people are shown by their alias where they have one
            var avatarPath = _avatarCache == null ? null : await _avatarCache.GetPathAsync(message.UserName);
            var senderName = thread.Participants.FirstOrDefault(p => p.Id == message.UserId)?.Name ?? message.UserName;
            var threadTitle = string.IsNullOrWhiteSpace(thread.Name) ? vm.OtherParticipantNames : thread.Name;

            _notificationService?.NotifyChatMessage(
                threadTitle,
                senderName,
                message.Content,
                avatarPath,
                () => OpenThread(vm));
        };

        return vm;
    }

    /// <summary>Shows the chat window on the given conversation</summary>
    private void OpenThread(ChatThreadViewModel threadVm)
    {
        IsCreatingThread = false;

        // It may have been replaced by a reload since the notification went out
        SelectedThread = Threads.FirstOrDefault(t => t.Thread.Id == threadVm.Thread.Id) ?? threadVm;

        OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedThreadChanged(ChatThreadViewModel? oldValue, ChatThreadViewModel? newValue)
    {
        SendMessageCommand.NotifyCanExecuteChanged();

        // The box is shared, so whatever was being written was for the thread being left
        _typingNotifier?.Stop();

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

    partial void OnMessageInputChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();

        // Emptying the box (sending clears it) counts as having stopped
        if (SelectedThread == null || string.IsNullOrWhiteSpace(value))
            _typingNotifier?.Stop();
        else
            _typingNotifier?.Typed(SelectedThread.Thread.Id);
    }

    // ── Delete thread ────────────────────────────────────────────────────────

    /// <summary>
    /// Takes a conversation off this user's list. The others in it keep it, and the server
    /// hands it back as an added thread if someone sends a new message.
    /// </summary>
    [RelayCommand]
    private async Task DeleteThreadAsync(ChatThreadViewModel? threadVm)
    {
        if (_chatClient == null || threadVm == null)
            return;

        try
        {
            await _chatClient.HideThreadAsync(threadVm.Thread.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chat thread {ThreadId}", threadVm.Thread.Id);

            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var index = Threads.IndexOf(threadVm);
            var wasSelected = SelectedThread == threadVm;

            Threads.Remove(threadVm);

            OnPropertyChanged(nameof(HasThreads));
            UpdateTotalUnreadCount();

            // Deleting the open one moves on to the next conversation down, or the one above if it was the last
            if (wasSelected)
                SelectedThread = Threads.Count == 0 ? null : Threads[Math.Clamp(index, 0, Threads.Count - 1)];
        });
    }

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

    /// <summary>The other participants one by one, so each name can show whether they are playing.</summary>
    public List<ParticipantName> OtherParticipantNameList { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    private int _unreadCount;

    public bool HasUnread => UnreadCount > 0;

    /// <summary>Who among the others is typing in this thread</summary>
    public ChatTypingState Typing { get; } = new();

    /// <summary>"Alice is typing…" and the like, or null while nobody is</summary>
    [ObservableProperty]
    private string? _typingText;

    public ChatThreadViewModel(ChatThread thread, Guid currentUserId)
    {
        Thread = thread;
        OtherParticipants = thread.Participants
            .Where(p => p.Id != currentUserId)
            .ToList();

        Typing.Changed += () => Dispatcher.UIThread.Post(() => TypingText = Typing.Describe(OtherParticipants));

        OtherParticipantNameList = OtherParticipants
            .Select((p, i) => new ParticipantName(p.Id, p.Name, i < OtherParticipants.Count - 1 ? ", " : string.Empty))
            .ToList();
    }
}

/// <summary>One name in a list of participants, with what follows it.</summary>
public sealed record ParticipantName(Guid UserId, string Name, string Separator);

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
