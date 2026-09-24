using System.ComponentModel;
using Chronvyr.Models;

namespace Chronvyr.Services;

/// <summary>
/// 多语言服务（FR-1.5）：支持简体中文 / English / 日本語。
/// 用法：XAML 中 <c>Text="{x:Bind Loc[KeyName]}"</c>，代码中 <c>Loc["KeyName"]</c>。
/// 切换语言后触发 <see cref="LanguageChanged"/>，由主窗口重建当前页面以刷新所有绑定。
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> Chinese = new Dictionary<string, string>
    {
        // 导航与通用
        ["AppName"] = "Chronvyr",
        ["NavHome"] = "首页",
        ["NavCalendar"] = "日历",
        ["NavSchedule"] = "日程",
        ["NavDiary"] = "日记",
        ["NavPomodoro"] = "番茄钟",
        ["NavWorkbench"] = "工作台",
        ["NavSettings"] = "设置",
        ["ActionSave"] = "保存",
        ["ActionCancel"] = "取消",
        ["ActionDelete"] = "删除",
        ["ActionEdit"] = "编辑",
        ["ActionCreate"] = "创建",
        ["ActionNew"] = "新建",
        ["Greeting"] = "你好，{0}",
        ["GreetingDawn"] = "夜深了，注意休息，{0}",
        ["GreetingEarlyMorning"] = "早上好，{0}",
        ["GreetingMorning"] = "上午好，{0}",
        ["GreetingNoon"] = "中午好，{0}",
        ["GreetingAfternoon"] = "下午好，{0}",
        ["GreetingEvening"] = "傍晚好，{0}",
        ["GreetingNight"] = "晚上好，{0}",
        ["GreetingLateNight"] = "夜深了，早点休息，{0}",
        ["WindowMinimize"] = "最小化",
        ["WindowMaximize"] = "最大化",
        ["WindowRestore"] = "还原",
        ["WindowClose"] = "关闭到托盘",

        // 日历
        ["CalendarTitle"] = "日历",
        ["CalendarToday"] = "今天",
        ["CalendarNoSchedule"] = "这一天还没有日程",
        ["HolidayRest"] = "休息",
        ["HolidayMakeup"] = "调休",
        ["HolidayTipOff"] = "法定放假日",
        ["HolidayTipWork"] = "调休上班日（周末补班）",
        ["HolidayOffline"] = "当前离线，节假日为本地缓存数据",
        ["HolidayRefreshTip"] = "重新联网获取节假日数据",
        ["HolidayFailed"] = "未能获取节假日数据",

        // 日程
        ["ScheduleTitle"] = "日程",
        ["ScheduleSummary"] = "共 {0} 项 · 未完成 {1} 项",
        ["ScheduleNew"] = "新建日程",
        ["ScheduleFilterAll"] = "全部",
        ["ScheduleFilterPending"] = "未完成",
        ["ScheduleFilterCompleted"] = "已完成",
        ["ScheduleEmpty"] = "这里还没有日程，点击右上角「新建日程」开始",
        ["ScheduleEditTitle"] = "新建日程",
        ["ScheduleEditTitleEdit"] = "编辑日程",
        ["FieldTitle"] = "标题",
        ["FieldDescription"] = "描述",
        ["FieldDueDate"] = "截止日期",
        ["FieldDueTime"] = "截止时间",
        ["FieldTitlePlaceholder"] = "要做什么？",
        ["FieldDescPlaceholder"] = "补充说明（可选）",
        ["FieldCompleted"] = "已完成",
        ["DetailTitle"] = "日程详情",
        ["ConfirmDeleteSchedule"] = "确定要删除「{0}」吗？此操作无法撤销。",
        ["ConfirmDeleteTitle"] = "删除日程",

        // 日记
        ["DiaryTitle"] = "日记",
        ["DiaryNew"] = "新建日记",
        ["DiaryEmpty"] = "还没有日记",
        ["DiaryNoSelection"] = "从左侧选择一篇日记，或点击右上角「新建日记」",
        ["DiaryUntitled"] = "未命名日记",
        ["DiaryDetailTitle"] = "日记详情",
        ["DiaryContentPlaceholder"] = "写下今天…",
        ["DiaryDeleteButton"] = "删除这篇日记",
        ["DiarySavedAt"] = "已于 {0} 保存",
        ["ConfirmDeleteDiary"] = "确定要删除「{0}」吗？此操作无法撤销。",
        ["ConfirmDeleteTitleDiary"] = "删除日记",
        ["FieldDate"] = "日期",
        ["FieldStartDate"] = "开始日期",
        ["FieldStartTime"] = "开始时间",
        ["FieldContent"] = "内容",
        ["ScheduleBoardEmpty"] = "还没有日程",
        ["ScheduleMarkCompletedTip"] = "标记完成 / 未完成",
        ["ScheduleEditTip"] = "编辑",
        ["ScheduleDeleteTip"] = "删除",

        // 番茄钟
        ["PomodoroTitle"] = "番茄钟",
        ["PomodoroTaskSection"] = "专注任务",
        ["PomodoroTaskLabel"] = "任务",
        ["PomodoroTaskPlaceholder"] = "打字写下我要完成的任务",
        ["PomodoroPickSchedule"] = "从未完成日程中选择",
        ["PomodoroNoPending"] = "暂无未完成日程",
        ["PomodoroDuration"] = "专注时长（分钟）",
        ["PomodoroStart"] = "开始专注",
        ["PomodoroResume"] = "继续",
        ["PomodoroAgain"] = "再来一轮",
        ["PomodoroPause"] = "暂停",
        ["PomodoroReset"] = "重置",
        ["PomodoroExit"] = "退出专注",
        ["PomodoroReady"] = "准备开始",
        ["PomodoroRunning"] = "专注中",
        ["PomodoroPaused"] = "已暂停",
        ["PomodoroFinished"] = "本轮完成",
        ["PomodoroDoneTitle"] = "番茄钟完成",
        ["PomodoroDoneMessage"] = "本轮专注已结束，休息一下吧。",

        // 工作台
        ["WorkbenchTitle"] = "工作台",
        ["WorkbenchNew"] = "新建项目",
        ["WorkbenchEmpty"] = "还没有项目，点击右上角「新建项目」开始管理",
        ["WorkbenchLatest"] = "最近更新",
        ["WorkbenchNoLog"] = "暂无更新日志",
        ["WorkbenchNoVersion"] = "未设置版本号",
        ["ProjectName"] = "项目名称",
        ["ProjectVersion"] = "当前版本号",
        ["ProjectProgress"] = "开发进度（%）",
        ["ProjectLogs"] = "更新日志",
        ["ProjectAddLog"] = "添加日志条目",
        ["ProjectNoLogEntry"] = "还没有更新日志，点击下方按钮添加。",
        ["ProjectLogPlaceholder"] = "这次更新了什么？",
        ["ProjectNew"] = "新建项目",
        ["ProjectEdit"] = "编辑项目",
        ["ConfirmDeleteProject"] = "确定要删除「{0}」及其全部更新日志吗？此操作无法撤销。",

        // 设置
        ["SettingsTitle"] = "设置",
        ["SettingsStyle"] = "视觉风格",
        ["StyleFollowSystem"] = "跟随系统",
        ["StyleLight"] = "浅色",
        ["StyleDark"] = "深色",
        ["SettingsWidgets"] = "桌面控件",
        ["SettingsWidgetsHint"] = "选择在桌面创建哪些控件，并可以为每个控件单独设置视觉风格与显示位置（X / Y 为相对主屏左上角的像素）。",
        ["WidgetUseMainStyle"] = "使用主界面风格",
        ["WidgetOwnStyle"] = "独立视觉风格",
        ["WidgetWidth"] = "宽度",
        ["WidgetTopGap"] = "与顶部的空隙",
        ["WidgetDragHint"] = "拖动标题栏即可移动，位置会自动记住。",
        ["WidgetCollapseTip"] = "折叠 / 展开",
        ["WidgetDiaryBar"] = "日记栏",
        ["WidgetScheduleBoard"] = "日程表",
        ["WidgetDynamicIsland"] = "胶囊栏",
        ["SettingsStartup"] = "启动",
        ["SettingsAutoStart"] = "开机自动启动 Chronvyr",
        ["SettingsAutoStartOn"] = "已写入注册表，开机将自动启动。",
        ["SettingsAutoStartOff"] = "已从注册表移除，开机不再自动启动。",
        ["SettingsAutoStartFailed"] = "写入注册表失败，可能被安全软件拦截。",
        ["SettingsAutoStartIdle"] = "开机时不会自动启动 Chronvyr。",
        ["SettingsLanguage"] = "界面语言",
        ["LanguageChinese"] = "简体中文",
        ["LanguageEnglish"] = "English",
        ["LanguageJapanese"] = "日本語",

        // 桌面控件与托盘
        ["TrayOpen"] = "主界面",
        ["TraySettings"] = "设置",
        ["TrayExit"] = "退出",
        ["DiaryBarNewTip"] = "快捷新建日记",
        ["ScheduleBoardNewTip"] = "快捷新建日程",
        ["IslandCollapseHint"] = "再次点击顶部窄条可收起面板",
        ["IslandTodaySchedule"] = "今日日程",
        ["IslandNoScheduleToday"] = "今天没有日程",
        ["IslandRecentDiary"] = "最近日记",
        ["IslandNoDiary"] = "还没有日记",
        ["IslandNoProject"] = "还没有项目",
        ["IslandHardware"] = "硬件监控",
        ["DiskLabel"] = "磁盘",
        ["GpuLabel"] = "显卡",
        ["IslandWeather"] = "天气",
        ["IslandClipboard"] = "剪切板历史",
        ["IslandClipboardEmpty"] = "暂无记录",
        ["WeatherUnavailableOnline"] = "天气获取失败",
        ["WeatherUnavailableOffline"] = "离线，天气不可用",
        ["WeatherLocating"] = "定位中…",
        ["BatteryNone"] = "无电池（台式机）",
        ["BatteryCharging"] = "充电中",
        ["BatteryDischarging"] = "使用中",
        ["MemoryLabel"] = "内存",
        ["CpuLabel"] = "CPU",
        ["IslandPending"] = "待完成 {0} 日程",
        ["ClipboardCopyTip"] = "复制",
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>
    {
        ["AppName"] = "Chronvyr",
        ["NavHome"] = "Home",
        ["NavCalendar"] = "Calendar",
        ["NavSchedule"] = "Schedule",
        ["NavDiary"] = "Diary",
        ["NavPomodoro"] = "Pomodoro",
        ["NavWorkbench"] = "Workbench",
        ["NavSettings"] = "Settings",
        ["ActionSave"] = "Save",
        ["ActionCancel"] = "Cancel",
        ["ActionDelete"] = "Delete",
        ["ActionEdit"] = "Edit",
        ["ActionCreate"] = "Create",
        ["ActionNew"] = "New",
        ["Greeting"] = "Hello, {0}",
        ["GreetingDawn"] = "It's late, {0} — get some rest",
        ["GreetingEarlyMorning"] = "Good morning, {0}",
        ["GreetingMorning"] = "Good morning, {0}",
        ["GreetingNoon"] = "Good afternoon, {0}",
        ["GreetingAfternoon"] = "Good afternoon, {0}",
        ["GreetingEvening"] = "Good evening, {0}",
        ["GreetingNight"] = "Good evening, {0}",
        ["GreetingLateNight"] = "It's late, {0} — time to rest",
        ["WindowMinimize"] = "Minimize",
        ["WindowMaximize"] = "Maximize",
        ["WindowRestore"] = "Restore",
        ["WindowClose"] = "Close to tray",

        ["CalendarTitle"] = "Calendar",
        ["CalendarToday"] = "Today",
        ["CalendarNoSchedule"] = "No schedule for this day",
        ["HolidayRest"] = "Rest",
        ["HolidayMakeup"] = "Work",
        ["HolidayTipOff"] = "Public holiday",
        ["HolidayTipWork"] = "Make-up workday (weekend shift)",
        ["HolidayOffline"] = "Offline — using cached holiday data",
        ["HolidayRefreshTip"] = "Re-fetch holiday data online",
        ["HolidayFailed"] = "Could not fetch holiday data",

        ["ScheduleTitle"] = "Schedule",
        ["ScheduleSummary"] = "{0} total · {1} pending",
        ["ScheduleNew"] = "New schedule",
        ["ScheduleFilterAll"] = "All",
        ["ScheduleFilterPending"] = "Pending",
        ["ScheduleFilterCompleted"] = "Completed",
        ["ScheduleEmpty"] = "No schedules yet — click “New schedule” to start",
        ["ScheduleEditTitle"] = "New schedule",
        ["ScheduleEditTitleEdit"] = "Edit schedule",
        ["FieldTitle"] = "Title",
        ["FieldDescription"] = "Description",
        ["FieldDueDate"] = "Due date",
        ["FieldDueTime"] = "Due time",
        ["FieldTitlePlaceholder"] = "What needs to be done?",
        ["FieldDescPlaceholder"] = "Extra notes (optional)",
        ["FieldCompleted"] = "Completed",
        ["DetailTitle"] = "Schedule details",
        ["ConfirmDeleteSchedule"] = "Delete “{0}”? This cannot be undone.",
        ["ConfirmDeleteTitle"] = "Delete schedule",

        ["DiaryTitle"] = "Diary",
        ["DiaryNew"] = "New entry",
        ["DiaryEmpty"] = "No diary entries yet",
        ["DiaryNoSelection"] = "Pick an entry on the left, or create one from the top right",
        ["DiaryUntitled"] = "Untitled",
        ["DiaryDetailTitle"] = "Diary details",
        ["DiaryContentPlaceholder"] = "Write something…",
        ["DiaryDeleteButton"] = "Delete this entry",
        ["DiarySavedAt"] = "Saved at {0}",
        ["ConfirmDeleteDiary"] = "Delete “{0}”? This cannot be undone.",
        ["ConfirmDeleteTitleDiary"] = "Delete diary entry",
        ["FieldDate"] = "Date",
        ["FieldStartDate"] = "Start date",
        ["FieldStartTime"] = "Start time",
        ["FieldContent"] = "Content",
        ["ScheduleBoardEmpty"] = "No schedules yet",
        ["ScheduleMarkCompletedTip"] = "Mark complete / incomplete",
        ["ScheduleEditTip"] = "Edit",
        ["ScheduleDeleteTip"] = "Delete",

        ["PomodoroTitle"] = "Pomodoro",
        ["PomodoroTaskSection"] = "Focus task",
        ["PomodoroTaskLabel"] = "Task",
        ["PomodoroTaskPlaceholder"] = "Type the task you want to finish",
        ["PomodoroPickSchedule"] = "Pick from pending schedules",
        ["PomodoroNoPending"] = "No pending schedules",
        ["PomodoroDuration"] = "Focus length (minutes)",
        ["PomodoroStart"] = "Start focus",
        ["PomodoroResume"] = "Resume",
        ["PomodoroAgain"] = "Another round",
        ["PomodoroPause"] = "Pause",
        ["PomodoroReset"] = "Reset",
        ["PomodoroExit"] = "End focus",
        ["PomodoroReady"] = "Ready",
        ["PomodoroRunning"] = "Focusing",
        ["PomodoroPaused"] = "Paused",
        ["PomodoroFinished"] = "Finished",
        ["PomodoroDoneTitle"] = "Pomodoro finished",
        ["PomodoroDoneMessage"] = "This focus session is over — take a break.",

        ["WorkbenchTitle"] = "Workbench",
        ["WorkbenchNew"] = "New project",
        ["WorkbenchEmpty"] = "No projects yet — click “New project” to start",
        ["WorkbenchLatest"] = "Latest update",
        ["WorkbenchNoLog"] = "No changelog yet",
        ["WorkbenchNoVersion"] = "No version set",
        ["ProjectName"] = "Project name",
        ["ProjectVersion"] = "Current version",
        ["ProjectProgress"] = "Progress (%)",
        ["ProjectLogs"] = "Changelog",
        ["ProjectAddLog"] = "Add changelog entry",
        ["ProjectNoLogEntry"] = "No changelog yet — add one below.",
        ["ProjectLogPlaceholder"] = "What changed?",
        ["ProjectNew"] = "New project",
        ["ProjectEdit"] = "Edit project",
        ["ConfirmDeleteProject"] = "Delete “{0}” and all its changelog? This cannot be undone.",

        ["SettingsTitle"] = "Settings",
        ["SettingsStyle"] = "Visual style",
        ["StyleFollowSystem"] = "Follow system",
        ["StyleLight"] = "Light",
        ["StyleDark"] = "Dark",
        ["SettingsWidgets"] = "Desktop widgets",
        ["SettingsWidgetsHint"] = "Choose which widgets to create on the desktop. Each can have its own style and position (X / Y are pixels relative to the primary display's top-left corner).",
        ["WidgetUseMainStyle"] = "Use main style",
        ["WidgetOwnStyle"] = "Widget style",
        ["WidgetWidth"] = "Width",
        ["WidgetTopGap"] = "Gap from top",
        ["WidgetDragHint"] = "Drag the title bar to move it; the position is remembered.",
        ["WidgetCollapseTip"] = "Collapse / expand",
        ["WidgetDiaryBar"] = "Diary bar",
        ["WidgetScheduleBoard"] = "Schedule board",
        ["WidgetDynamicIsland"] = "Capsule bar",
        ["SettingsStartup"] = "Startup",
        ["SettingsAutoStart"] = "Launch Chronvyr at sign-in",
        ["SettingsAutoStartOn"] = "Added to the registry — Chronvyr will start automatically.",
        ["SettingsAutoStartOff"] = "Removed from the registry — Chronvyr will not auto-start.",
        ["SettingsAutoStartFailed"] = "Failed to write the registry — blocked by security software?",
        ["SettingsAutoStartIdle"] = "Chronvyr will not start automatically.",
        ["SettingsLanguage"] = "Language",
        ["LanguageChinese"] = "简体中文",
        ["LanguageEnglish"] = "English",
        ["LanguageJapanese"] = "日本語",

        ["TrayOpen"] = "Main window",
        ["TraySettings"] = "Settings",
        ["TrayExit"] = "Exit",
        ["DiaryBarNewTip"] = "Quick new entry",
        ["ScheduleBoardNewTip"] = "Quick new schedule",
        ["IslandCollapseHint"] = "Click the bar again to collapse",
        ["IslandTodaySchedule"] = "Today",
        ["IslandNoScheduleToday"] = "Nothing scheduled today",
        ["IslandRecentDiary"] = "Recent diary",
        ["IslandNoDiary"] = "No diary entries yet",
        ["IslandNoProject"] = "No projects yet",
        ["IslandHardware"] = "Hardware",
        ["DiskLabel"] = "Disk",
        ["GpuLabel"] = "GPU",
        ["IslandWeather"] = "Weather",
        ["IslandClipboard"] = "Clipboard history",
        ["IslandClipboardEmpty"] = "No records",
        ["WeatherUnavailableOnline"] = "Weather unavailable",
        ["WeatherUnavailableOffline"] = "Offline — weather unavailable",
        ["WeatherLocating"] = "Locating…",
        ["BatteryNone"] = "No battery (desktop)",
        ["BatteryCharging"] = "charging",
        ["BatteryDischarging"] = "on battery",
        ["MemoryLabel"] = "RAM",
        ["CpuLabel"] = "CPU",
        ["IslandPending"] = "{0} pending",
        ["ClipboardCopyTip"] = "Copy",
    };

    private static readonly IReadOnlyDictionary<string, string> Japanese = new Dictionary<string, string>
    {
        ["AppName"] = "Chronvyr",
        ["NavHome"] = "ホーム",
        ["NavCalendar"] = "カレンダー",
        ["NavSchedule"] = "予定",
        ["NavDiary"] = "日記",
        ["NavPomodoro"] = "ポモドーロ",
        ["NavWorkbench"] = "ワークベンチ",
        ["NavSettings"] = "設定",
        ["ActionSave"] = "保存",
        ["ActionCancel"] = "キャンセル",
        ["ActionDelete"] = "削除",
        ["ActionEdit"] = "編集",
        ["ActionCreate"] = "作成",
        ["ActionNew"] = "新規",
        ["Greeting"] = "こんにちは、{0}",
        ["GreetingDawn"] = "夜更けです、{0} さん、そろそろ休みましょう",
        ["GreetingEarlyMorning"] = "おはようございます、{0} さん",
        ["GreetingMorning"] = "おはようございます、{0} さん",
        ["GreetingNoon"] = "こんにちは、{0} さん",
        ["GreetingAfternoon"] = "こんにちは、{0} さん",
        ["GreetingEvening"] = "こんばんは、{0} さん",
        ["GreetingNight"] = "こんばんは、{0} さん",
        ["GreetingLateNight"] = "夜更けです、{0} さん、お休みなさい",
        ["WindowMinimize"] = "最小化",
        ["WindowMaximize"] = "最大化",
        ["WindowRestore"] = "元に戻す",
        ["WindowClose"] = "トレイに格納",

        ["CalendarTitle"] = "カレンダー",
        ["CalendarToday"] = "今日",
        ["CalendarNoSchedule"] = "この日の予定はありません",
        ["HolidayRest"] = "休み",
        ["HolidayMakeup"] = "振替",
        ["HolidayTipOff"] = "祝日",
        ["HolidayTipWork"] = "振替出勤日（週末の振替）",
        ["HolidayOffline"] = "オフライン — キャッシュされた祝日データ",
        ["HolidayRefreshTip"] = "祝日データを再取得",
        ["HolidayFailed"] = "祝日データを取得できませんでした",

        ["ScheduleTitle"] = "予定",
        ["ScheduleSummary"] = "全 {0} 件 · 未完了 {1} 件",
        ["ScheduleNew"] = "予定を作成",
        ["ScheduleFilterAll"] = "すべて",
        ["ScheduleFilterPending"] = "未完了",
        ["ScheduleFilterCompleted"] = "完了",
        ["ScheduleEmpty"] = "予定はまだありません — 右上の「予定を作成」から追加できます",
        ["ScheduleEditTitle"] = "予定を作成",
        ["ScheduleEditTitleEdit"] = "予定を編集",
        ["FieldTitle"] = "タイトル",
        ["FieldDescription"] = "説明",
        ["FieldDueDate"] = "期限日",
        ["FieldDueTime"] = "期限時刻",
        ["FieldTitlePlaceholder"] = "何をしますか？",
        ["FieldDescPlaceholder"] = "補足（任意）",
        ["FieldCompleted"] = "完了",
        ["DetailTitle"] = "予定の詳細",
        ["ConfirmDeleteSchedule"] = "「{0}」を削除しますか？元に戻せません。",
        ["ConfirmDeleteTitle"] = "予定を削除",

        ["DiaryTitle"] = "日記",
        ["DiaryNew"] = "日記を書く",
        ["DiaryEmpty"] = "日記はまだありません",
        ["DiaryNoSelection"] = "左から日記を選ぶか、右上から新規作成してください",
        ["DiaryUntitled"] = "無題",
        ["DiaryDetailTitle"] = "日記の詳細",
        ["DiaryContentPlaceholder"] = "今日のことを書きましょう…",
        ["DiaryDeleteButton"] = "この日記を削除",
        ["DiarySavedAt"] = "{0} に保存しました",
        ["ConfirmDeleteDiary"] = "「{0}」を削除しますか？元に戻せません。",
        ["ConfirmDeleteTitleDiary"] = "日記を削除",
        ["FieldDate"] = "日付",
        ["FieldStartDate"] = "開始日",
        ["FieldStartTime"] = "開始時刻",
        ["FieldContent"] = "内容",
        ["ScheduleBoardEmpty"] = "予定はまだありません",
        ["ScheduleMarkCompletedTip"] = "完了 / 未完了を切り替え",
        ["ScheduleEditTip"] = "編集",
        ["ScheduleDeleteTip"] = "削除",

        ["PomodoroTitle"] = "ポモドーロ",
        ["PomodoroTaskSection"] = "集中タスク",
        ["PomodoroTaskLabel"] = "タスク",
        ["PomodoroTaskPlaceholder"] = "完了したいタスクを入力",
        ["PomodoroPickSchedule"] = "未完了の予定から選ぶ",
        ["PomodoroNoPending"] = "未完了の予定はありません",
        ["PomodoroDuration"] = "集中時間（分）",
        ["PomodoroStart"] = "集中を開始",
        ["PomodoroResume"] = "再開",
        ["PomodoroAgain"] = "もう一度",
        ["PomodoroPause"] = "一時停止",
        ["PomodoroReset"] = "リセット",
        ["PomodoroExit"] = "集中を終了",
        ["PomodoroReady"] = "準備完了",
        ["PomodoroRunning"] = "集中中",
        ["PomodoroPaused"] = "一時停止中",
        ["PomodoroFinished"] = "完了",
        ["PomodoroDoneTitle"] = "ポモドーロ完了",
        ["PomodoroDoneMessage"] = "集中時間が終わりました。休憩しましょう。",

        ["WorkbenchTitle"] = "ワークベンチ",
        ["WorkbenchNew"] = "プロジェクトを作成",
        ["WorkbenchEmpty"] = "プロジェクトはまだありません",
        ["WorkbenchLatest"] = "最近の更新",
        ["WorkbenchNoLog"] = "更新履歴なし",
        ["WorkbenchNoVersion"] = "バージョン未設定",
        ["ProjectName"] = "プロジェクト名",
        ["ProjectVersion"] = "現在のバージョン",
        ["ProjectProgress"] = "進捗（%）",
        ["ProjectLogs"] = "更新履歴",
        ["ProjectAddLog"] = "履歴を追加",
        ["ProjectNoLogEntry"] = "更新履歴はまだありません。",
        ["ProjectLogPlaceholder"] = "何を更新しましたか？",
        ["ProjectNew"] = "プロジェクトを作成",
        ["ProjectEdit"] = "プロジェクトを編集",
        ["ConfirmDeleteProject"] = "「{0}」とその更新履歴を削除しますか？元に戻せません。",

        ["SettingsTitle"] = "設定",
        ["SettingsStyle"] = "外観スタイル",
        ["StyleFollowSystem"] = "システムに従う",
        ["StyleLight"] = "ライト",
        ["StyleDark"] = "ダーク",
        ["SettingsWidgets"] = "デスクトップウィジェット",
        ["SettingsWidgetsHint"] = "デスクトップに表示するウィジェットを選択します。それぞれ個別のスタイルと位置を設定できます（X / Y はプライマリ画面左上からのピクセル）。",
        ["WidgetUseMainStyle"] = "メインスタイルを使用",
        ["WidgetOwnStyle"] = "個別スタイル",
        ["WidgetWidth"] = "幅",
        ["WidgetTopGap"] = "上端からの余白",
        ["WidgetDragHint"] = "タイトルバーをドラッグすると移動できます。位置は記憶されます。",
        ["WidgetCollapseTip"] = "折りたたむ / 展開",
        ["WidgetDiaryBar"] = "日記バー",
        ["WidgetScheduleBoard"] = "予定ボード",
        ["WidgetDynamicIsland"] = "カプセルバー",
        ["SettingsStartup"] = "起動",
        ["SettingsAutoStart"] = "サインイン時に Chronvyr を自動起動",
        ["SettingsAutoStartOn"] = "レジストリに登録しました。自動起動します。",
        ["SettingsAutoStartOff"] = "レジストリから削除しました。自動起動しません。",
        ["SettingsAutoStartFailed"] = "レジストリへの書き込みに失敗しました。",
        ["SettingsAutoStartIdle"] = "自動起動はオフです。",
        ["SettingsLanguage"] = "表示言語",
        ["LanguageChinese"] = "简体中文",
        ["LanguageEnglish"] = "English",
        ["LanguageJapanese"] = "日本語",

        ["TrayOpen"] = "メイン画面",
        ["TraySettings"] = "設定",
        ["TrayExit"] = "終了",
        ["DiaryBarNewTip"] = "日記をすばやく作成",
        ["ScheduleBoardNewTip"] = "予定をすばやく作成",
        ["IslandCollapseHint"] = "バーをもう一度クリックすると閉じます",
        ["IslandTodaySchedule"] = "今日の予定",
        ["IslandNoScheduleToday"] = "今日の予定はありません",
        ["IslandRecentDiary"] = "最近の日記",
        ["IslandNoDiary"] = "日記はまだありません",
        ["IslandNoProject"] = "プロジェクトはまだありません",
        ["IslandHardware"] = "ハードウェア",
        ["DiskLabel"] = "ディスク",
        ["GpuLabel"] = "GPU",
        ["IslandWeather"] = "天気",
        ["IslandClipboard"] = "クリップボード履歴",
        ["IslandClipboardEmpty"] = "記録なし",
        ["WeatherUnavailableOnline"] = "天気を取得できません",
        ["WeatherUnavailableOffline"] = "オフライン — 天気は利用できません",
        ["WeatherLocating"] = "位置を取得中…",
        ["BatteryNone"] = "バッテリーなし（デスクトップ）",
        ["BatteryCharging"] = "充電中",
        ["BatteryDischarging"] = "バッテリー駆動",
        ["MemoryLabel"] = "メモリ",
        ["CpuLabel"] = "CPU",
        ["IslandPending"] = "未完了 {0} 件",
        ["ClipboardCopyTip"] = "コピー",
    };

    private AppLanguage _language = AppLanguage.ChineseSimplified;

    /// <summary>
    /// 繁體中文表：直接由简体表经系统 <c>LCMapStringEx</c> 转换而来，
    /// 不需要人工维护第二份翻译，也不会出现简繁不一致。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Traditional = BuildTraditional();

    /// <summary>全局单例（界面绑定用）。</summary>
    public static LocalizationService Instance { get; private set; } = null!;

    /// <summary>创建并设为全局实例。</summary>
    public LocalizationService()
    {
        Instance = this;
    }

    /// <summary>按索引切换语言（0 中文 / 1 English / 2 日本語）。</summary>
    public static void Apply(int languageIndex) =>
        Instance.Language = (AppLanguage)Math.Clamp(languageIndex, 0, 3);

    /// <summary>语言变化（主窗口据此重建页面）。</summary>
    public event EventHandler? LanguageChanged;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>当前语言。</summary>
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;

            // 同步线程区域性，让模型层的日期/时间文本（DueTimeText、DateText 等）
            // 自动跟随界面语言，无需各处手动传 CultureInfo。
            var culture = value switch
            {
                AppLanguage.ChineseTraditional => new System.Globalization.CultureInfo("zh-TW"),
                AppLanguage.English => new System.Globalization.CultureInfo("en-US"),
                AppLanguage.Japanese => new System.Globalization.CultureInfo("ja-JP"),
                _ => new System.Globalization.CultureInfo("zh-CN"),
            };
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>取本地化文本（缺失时回退中文，再回退键名）。</summary>
    public string this[string key]
    {
        get
        {
            var table = Table(_language);
            if (table.TryGetValue(key, out var value))
            {
                return value;
            }

            return Chinese.TryGetValue(key, out var fallback) ? fallback : key;
        }
    }

    /// <summary>
    /// 供 XAML 函数绑定使用：<c>Text="{x:Bind Loc.T('NavHome')}"</c>。
    /// 之所以不用索引器，是因为 WinUI 的 x:Bind 不支持字符串索引器
    /// （编译器会在 ExitPathStringIndexer 处内部报错）。
    /// </summary>
    public string T(string key) => this[key];

    /// <summary>取本地化文本并格式化。</summary>
    public string Format(string key, params object?[] args)
    {
        try
        {
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, this[key], args);
        }
        catch (FormatException)
        {
            return this[key];
        }
    }

    /// <summary>按系统语言猜一个初始值。</summary>
    public static AppLanguage DetectSystemLanguage()
    {
        var name = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return AppLanguage.Japanese;
        }

        if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            // zh-TW / zh-HK / zh-MO 视为繁體。
            return name.Contains("TW", StringComparison.OrdinalIgnoreCase)
                || name.Contains("HK", StringComparison.OrdinalIgnoreCase)
                || name.Contains("MO", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Hant", StringComparison.OrdinalIgnoreCase)
                ? AppLanguage.ChineseTraditional
                : AppLanguage.ChineseSimplified;
        }

        return name.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.English
            : AppLanguage.ChineseSimplified;
    }

    private static IReadOnlyDictionary<string, string> Table(AppLanguage language) => language switch
    {
        AppLanguage.ChineseTraditional => Traditional,
        AppLanguage.English => English,
        AppLanguage.Japanese => Japanese,
        _ => Chinese,
    };

    private static IReadOnlyDictionary<string, string> BuildTraditional()
    {
        var table = new Dictionary<string, string>(Chinese.Count);
        foreach (var pair in Chinese)
        {
            table[pair.Key] = ToTraditional(pair.Value);
        }

        return table;
    }

    /// <summary>用系统 API 把简体文本转成繁体。</summary>
    private static string ToTraditional(string simplified)
    {
        if (string.IsNullOrEmpty(simplified))
        {
            return simplified;
        }

        try
        {
            // 预留两倍长度：个别字符转换后长度可能变化。
            var buffer = new char[simplified.Length * 2 + 2];
            var written = LCMapStringEx(
                "zh-CN",
                LcMapTraditionalChinese,
                simplified,
                simplified.Length,
                buffer,
                buffer.Length,
                0,
                0,
                0);

            return written > 0 ? new string(buffer, 0, written) : simplified;
        }
        catch (DllNotFoundException)
        {
            return simplified;
        }
        catch (EntryPointNotFoundException)
        {
            return simplified;
        }
    }

    private const uint LcMapTraditionalChinese = 0x04000000;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int LCMapStringEx(
        string lpLocaleName,
        uint dwMapFlags,
        string lpSrcStr,
        int cchSrc,
        [System.Runtime.InteropServices.Out] char[] lpDestStr,
        int cchDest,
        nint lpVersionInformation,
        nint lpReserved,
        nint sortHandle);
}
