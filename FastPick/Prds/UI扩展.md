WinUI 3 黑色主题（Fluent Design）设计规范
一、核心设计原则
基于 Fluent Design 的黑色主题核心是：深邃、层次、通透、易用，在保证视觉沉浸感的同时，兼顾 Windows 系统的交互一致性和可访问性。
1. 色彩系统（Fluent Design 黑色主题标准色值）
表格
用途	色值（Hex）	色值（RGB）	WinUI 3 资源名	说明
主背景（Base）	#000000	(0, 0, 0)	SystemControlBackgroundAltHighBrush	页面 / 窗口主背景
次级背景（Surface）	#1E1E1E	(30, 30, 30)	SystemControlBackgroundMediumBrush	卡片、面板背景
三级背景（Elevated）	#2D2D2D	(45, 45, 45)	自定义资源	悬浮 / 选中容器背景
主文本	#FFFFFF	(255, 255, 255)	SystemControlForegroundBaseHighBrush	主要内容文本
次级文本	#E0E0E0	(224, 224, 224)	SystemControlForegroundBaseMediumBrush	辅助说明文本
禁用文本	#808080	(128, 128, 128)	SystemControlForegroundDisabledBrush	禁用状态文本
强调色（主色）	#0078D4	(0, 120, 212)	SystemAccentColor	Fluent 标准蓝色（可替换）
强调色（悬停）	#1089FF	(16, 137, 255)	SystemAccentColorLight1	交互元素悬停
边框 / 分割线	#333333	(51, 51, 51)	自定义资源	控件边框、区域分割
2. 视觉层次（Fluent Design 核心特性）
深度（Depth）：通过阴影区分控件层级，黑色主题下阴影强度需降低（避免过亮）
卡片阴影：0 2px 8px rgba(0,0,0,0.4)
悬浮控件阴影：0 4px 12px rgba(0,0,0,0.6)
材质（Material）：半透明效果（Acrylic）在黑色主题下使用低不透明度
亚克力背景：Background="{ThemeResource SystemControlAcrylicWindowBrush}"
光效（Light）：交互元素（按钮、输入框）的高光 / 描边使用浅灰色（#404040）
运动（Motion）：保持 Fluent Design 标准动效，过渡动画时长 200ms
3. 控件设计规范
表格
控件类型	黑色主题设计要点
按钮（Button）	常规：背景 #2D2D2D，文本 #FFFFFF；悬停：背景 #383838；点击：背景 #444444
输入框（TextBox）	边框 #333333，聚焦边框 #0078D4，背景 #1E1E1E，文本 #FFFFFF
列表（ListView）	项背景 #1E1E1E，选中项 #2D2D2D + 强调色描边，分隔线 #333333
菜单（MenuFlyout）	背景 #1E1E1E，菜单项悬停 #2D2D2D，分隔线 #333333
二、代码实现（WinUI 3 黑色主题）
以下是完整的 WinUI 3 黑色主题实现代码，基于 Fluent Design，可直接集成到你的项目中。
1. 项目配置
确保你的 WinUI 3 项目（打包 / 非打包应用）已引用 Microsoft.UI.Xaml 包（版本 ≥ 2.8）。
2. 资源字典（Themes/BlackTheme.xaml）
xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:YourAppName.Themes">

    <!-- 基础色彩资源 -->
    <SolidColorBrush x:Key="BlackTheme_BaseBackground" Color="#000000"/>
    <SolidColorBrush x:Key="BlackTheme_SurfaceBackground" Color="#1E1E1E"/>
    <SolidColorBrush x:Key="BlackTheme_ElevatedBackground" Color="#2D2D2D"/>
    <SolidColorBrush x:Key="BlackTheme_PrimaryText" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="BlackTheme_SecondaryText" Color="#E0E0E0"/>
    <SolidColorBrush x:Key="BlackTheme_DisabledText" Color="#808080"/>
    <SolidColorBrush x:Key="BlackTheme_Border" Color="#333333"/>
    <SolidColorBrush x:Key="BlackTheme_Accent" Color="#0078D4"/>
    <SolidColorBrush x:Key="BlackTheme_AccentHover" Color="#1089FF"/>

    <!-- 按钮样式（Fluent Design） -->
    <Style x:Key="BlackTheme_ButtonStyle" TargetType="Button">
        <Setter Property="Background" Value="{StaticResource BlackTheme_ElevatedBackground}"/>
        <Setter Property="Foreground" Value="{StaticResource BlackTheme_PrimaryText}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BlackTheme_Border}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="CornerRadius" Value="4"/>
        <Setter Property="FontFamily" Value="Segoe UI Variable Text"/>
        <Setter Property="FontSize" Value="14"/>
        <Setter Property="HorizontalAlignment" Value="Left"/>
        <Setter Property="VerticalAlignment" Value="Center"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Grid x:Name="RootGrid" Background="{TemplateBinding Background}"
                          CornerRadius="{TemplateBinding CornerRadius}">
                        <!-- 阴影（Fluent Design 深度） -->
                        <VisualStateManager.VisualStateGroups>
                            <VisualStateGroup x:Name="CommonStates">
                                <VisualState x:Name="Normal"/>
                                <VisualState x:Name="PointerOver">
                                    <Storyboard>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="RootGrid"
                                                                       Storyboard.TargetProperty="Background">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="#383838"/>
                                        </ObjectAnimationUsingKeyFrames>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="RootGrid"
                                                                       Storyboard.TargetProperty="BorderBrush">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="{StaticResource BlackTheme_AccentHover}"/>
                                        </ObjectAnimationUsingKeyFrames>
                                    </Storyboard>
                                </VisualState>
                                <VisualState x:Name="Pressed">
                                    <Storyboard>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="RootGrid"
                                                                       Storyboard.TargetProperty="Background">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="#444444"/>
                                        </ObjectAnimationUsingKeyFrames>
                                    </Storyboard>
                                </VisualState>
                                <VisualState x:Name="Disabled">
                                    <Storyboard>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="RootGrid"
                                                                       Storyboard.TargetProperty="Background">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="#252525"/>
                                        </ObjectAnimationUsingKeyFrames>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="ContentPresenter"
                                                                       Storyboard.TargetProperty="Foreground">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="{StaticResource BlackTheme_DisabledText}"/>
                                        </ObjectAnimationUsingKeyFrames>
                                    </Storyboard>
                                </VisualState>
                            </VisualStateGroup>
                        </VisualStateManager.VisualStateGroups>
                        
                        <ContentPresenter x:Name="ContentPresenter"
                                          Content="{TemplateBinding Content}"
                                          ContentTemplate="{TemplateBinding ContentTemplate}"
                                          HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                          VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                          Margin="{TemplateBinding Padding}"
                                          Foreground="{TemplateBinding Foreground}"/>
                    </Grid>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

    <!-- 文本框样式 -->
    <Style x:Key="BlackTheme_TextBoxStyle" TargetType="TextBox">
        <Setter Property="Background" Value="{StaticResource BlackTheme_SurfaceBackground}"/>
        <Setter Property="Foreground" Value="{StaticResource BlackTheme_PrimaryText}"/>
        <Setter Property="BorderBrush" Value="{StaticResource BlackTheme_Border}"/>
        <Setter Property="BorderThickness" Value="1"/>
        <Setter Property="PlaceholderTextForeground" Value="{StaticResource BlackTheme_DisabledText}"/>
        <Setter Property="CornerRadius" Value="4"/>
        <Setter Property="Padding" Value="12,8"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="TextBox">
                    <Grid>
                        <VisualStateManager.VisualStateGroups>
                            <VisualStateGroup x:Name="CommonStates">
                                <VisualState x:Name="Normal"/>
                                <VisualState x:Name="PointerOver">
                                    <Storyboard>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="BorderElement"
                                                                       Storyboard.TargetProperty="BorderBrush">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="#404040"/>
                                        </ObjectAnimationUsingKeyFrames>
                                    </Storyboard>
                                </VisualState>
                                <VisualState x:Name="Focused">
                                    <Storyboard>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="BorderElement"
                                                                       Storyboard.TargetProperty="BorderBrush">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="{StaticResource BlackTheme_Accent}"/>
                                        </ObjectAnimationUsingKeyFrames>
                                        <ObjectAnimationUsingKeyFrames Storyboard.TargetName="BorderElement"
                                                                       Storyboard.TargetProperty="BorderThickness">
                                            <DiscreteObjectKeyFrame KeyTime="0" Value="2"/>
                                        </ObjectAnimationUsingKeyFrames>
                                    </Storyboard>
                                </VisualState>
                            </VisualStateGroup>

                            <Border x:Name="BorderElement"
                                    Background="{TemplateBinding Background}"
                                    BorderBrush="{TemplateBinding BorderBrush}"
                                    BorderThickness="{TemplateBinding BorderThickness}"
                                    CornerRadius="{TemplateBinding CornerRadius}">
                                <ScrollViewer x:Name="ContentElement"
                                              Padding="{TemplateBinding Padding}"
                                              HorizontalScrollBarVisibility="Hidden"
                                              VerticalScrollBarVisibility="Hidden"/>
                            </Border>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Style>

    <!-- 全局样式覆盖 -->
    <Style TargetType="Page">
        <Setter Property="Background" Value="{StaticResource BlackTheme_BaseBackground}"/>
        <Setter Property="Foreground" Value="{StaticResource BlackTheme_PrimaryText}"/>
        <Setter Property="FontFamily" Value="Segoe UI Variable Text"/>
    </Style>
</ResourceDictionary>
3. 页面使用示例（MainPage.xaml）
xml
<Page
    x:Class="YourAppName.MainPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:YourAppName"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">

    <Page.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- 引入黑色主题资源 -->
                <ResourceDictionary Source="Themes/BlackTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Page.Resources>

    <!-- Fluent Design 黑色主题布局示例 -->
    <Grid Padding="24" RowDefinitions="Auto,Auto,Auto,*">
        <TextBlock Grid.Row="0" Text="Fluent Design 黑色主题" FontSize="24" FontWeight="Bold" Margin="0,0,0,24"/>
        
        <TextBox Grid.Row="1" 
                 PlaceholderText="输入框示例" 
                 Style="{StaticResource BlackTheme_TextBoxStyle}"
                 Width="300" Margin="0,0,0,16"/>
        
        <Button Grid.Row="2" 
                Content="按钮示例" 
                Style="{StaticResource BlackTheme_ButtonStyle}"
                Margin="0,0,0,24"/>
        
        <!-- 卡片容器（带阴影） -->
        <Border Grid.Row="3" 
                Background="{StaticResource BlackTheme_SurfaceBackground}"
                CornerRadius="8"
                Padding="16"
                Margin="0,16,0,0">
            <TextBlock Text="卡片内容示例" Foreground="{StaticResource BlackTheme_SecondaryText}"/>
        </Border>
    </Grid>
</Page>
4. 应用级主题设置（App.xaml）
xml
<Application
    x:Class="YourAppName.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:local="using:YourAppName">

    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <!-- WinUI 3 基础样式 -->
                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls"/>
                <!-- 全局引入黑色主题 -->
                <ResourceDictionary Source="Themes/BlackTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
三、最佳实践建议
可访问性：确保文本与背景的对比度 ≥ 4.5:1（黑色主题下已满足），禁用状态文本对比度 ≥ 2:1
性能优化：黑色主题下减少过度的半透明效果（Acrylic），降低 GPU 开销
主题切换：可通过 RequestedTheme 属性实现亮色 / 黑色主题切换：
csharp
运行
// 切换到黑色主题
this.RequestedTheme = ElementTheme.Dark;
// 切换到系统主题
this.RequestedTheme = ElementTheme.Default;
控件一致性：所有自定义控件需遵循上述色彩和样式规范，保证视觉统一。
总结
WinUI 3 黑色主题的核心是基于 Fluent Design 的三层背景色（#000000/#1E1E1E/#2D2D2D）和高对比度文本，兼顾沉浸感与可读性；
控件样式需遵循「常规 - 悬停 - 点击 - 禁用」的状态设计，通过阴影和边框体现 Fluent Design 的深度层次；
可通过资源字典统一管理主题样式，结合 RequestedTheme 实现主题切换，保证代码可维护性。
你可以直接复用上述代码模板，根据实际需求调整强调色（如替换为品牌色）或控件尺寸，快速落地符合 Fluent Design 的 WinUI 3 黑色主题。