using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PRM.Core.Resources.Controls
{
    public class ButtonEx : Button
    {
        /*
    <Style TargetType="{x:Type controls:ButtonEx}" x:Key="ButtonExStyleBase">
        <Setter Property="Focusable" Value="False"/>
        <Setter Property="FocusVisualStyle" Value="{x:Null}"></Setter>
        <Setter Property="Opacity" Value="1"></Setter>
        <Setter Property="Padding" Value="5"></Setter>
        <Setter Property="BorderBrush" Value="Gray"></Setter>
        <Setter Property="VerticalContentAlignment" Value="Center"/>
        <Setter Property="HorizontalContentAlignment" Value="Center"/>
        <Setter Property="Background" Value="{DynamicResource PrimaryMidBrush}"/>
        <Setter Property="Foreground" Value="{DynamicResource PrimaryTextBrush}"/>
        <Setter Property="BackgroundOnMouseOver" Value="{DynamicResource PrimaryLightBrush}"/>
        <Setter Property="ForegroundOnMouseOver" Value="{DynamicResource PrimaryTextBrush}"/>
        <Setter Property="OpacityOnMouseOver" Value="1"/>
        <Setter Property="BackgroundOnMouseDown" Value="{DynamicResource PrimaryLightBrush}"/>
        <Setter Property="ForegroundOnMouseDown" Value="{DynamicResource PrimaryTextBrush}"/>
        <Setter Property="OpacityOnMouseDown" Value="0.5"/>
        <Setter Property="SnapsToDevicePixels" Value="True"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type Button}">
                    <Grid>
                        <Border 
                        x:Name="ButtonBorder"
                        Background="{TemplateBinding Background}" 
                        BorderBrush="{TemplateBinding BorderBrush}" 
                        BorderThickness="{TemplateBinding BorderThickness}"
                        CornerRadius="{Binding BorderCornerRadius, RelativeSource={x:Static RelativeSource.TemplatedParent}}"
                        SnapsToDevicePixels="true">
                        </Border>
                        <ContentPresenter 
                            x:Name="ButtonContentPresenter"
                            HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" 
                            VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                            RecognizesAccessKey="True" 
                            SnapsToDevicePixels="{TemplateBinding SnapsToDevicePixels}" 
                            Margin="{TemplateBinding Padding}"/>
                    </Grid>
                    <ControlTemplate.Triggers>
                        <MultiTrigger>
                            <MultiTrigger.Conditions>
                                <Condition Property="IsMouseOver" Value="True"></Condition>
                                <Condition Property="IsEnabled" Value="True"></Condition>
                            </MultiTrigger.Conditions>
                            <MultiTrigger.Setters>
                                <Setter TargetName="ButtonBorder" Property="Background" Value="{Binding BackgroundOnMouseOver, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                                <Setter TargetName="ButtonBorder" Property="Opacity" Value="{Binding OpacityOnMouseOver, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                                <Setter TargetName="ButtonContentPresenter" Property="TextBlock.Foreground" Value="{Binding ForegroundOnMouseOver, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                            </MultiTrigger.Setters>
                        </MultiTrigger>
                        <MultiTrigger>
                            <MultiTrigger.Conditions>
                                <Condition Property="IsMouseOver" Value="True"></Condition>
                                <Condition Property="IsPressed" Value="True"></Condition>
                            </MultiTrigger.Conditions>
                            <MultiTrigger.Setters>
                                <Setter TargetName="ButtonBorder" Property="Background" Value="{Binding BackgroundOnMouseDown, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                                <Setter TargetName="ButtonBorder" Property="Opacity" Value="{Binding OpacityOnMouseDown, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                                <Setter TargetName="ButtonContentPresenter" Property="TextBlock.Foreground" Value="{Binding ForegroundOnMouseDown, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                            </MultiTrigger.Setters>
                        </MultiTrigger>
                        <Trigger Property="IsEnabled" Value="False">
                            <Setter TargetName="ButtonBorder" Property="Background" Value="{Binding BackgroundOnDisabled, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                            <Setter TargetName="ButtonBorder" Property="Opacity" Value="{Binding OpacityOnDisabled, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                            <Setter TargetName="ButtonContentPresenter" Property="TextBlock.Foreground" Value="{Binding ForegroundOnDisabled, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                        </Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <MultiTrigger>
                <MultiTrigger.Conditions>
                    <Condition Property="IsMouseOver" Value="True"></Condition>
                    <Condition Property="IsEnabled" Value="True"></Condition>
                </MultiTrigger.Conditions>
                <MultiTrigger.Setters>
                    <Setter Property="Foreground" Value="{Binding ForegroundOnMouseOver, RelativeSource={x:Static RelativeSource.Self}}"/>
                </MultiTrigger.Setters>
            </MultiTrigger>
            <MultiTrigger>
                <MultiTrigger.Conditions>
                    <Condition Property="IsMouseOver" Value="True"></Condition>
                    <Condition Property="IsPressed" Value="True"></Condition>
                </MultiTrigger.Conditions>
                <MultiTrigger.Setters>
                    <Setter Property="Foreground" Value="{Binding ForegroundOnMouseDown, RelativeSource={x:Static RelativeSource.Self}}"/>
                </MultiTrigger.Setters>
            </MultiTrigger>
            <Trigger Property="IsEnabled" Value="False">
                <Setter Property="Foreground" Value="{Binding ForegroundOnDisabled, RelativeSource={x:Static RelativeSource.Self}}"/>
            </Trigger>
        </Style.Triggers>
    </Style>

    <Style TargetType="{x:Type controls:ButtonEx}" BasedOn="{StaticResource ButtonExStyleBase}"/>
        */


        static ButtonEx()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonEx), new FrameworkPropertyMetadata(typeof(ButtonEx)));// Let ButtonEx use ButtonEx type style not
        }


        #region ForegroundOnMouseOver
        public static readonly DependencyProperty ForegroundOnMouseOverProperty = DependencyProperty.Register("ForegroundOnMouseOver", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Black));
        public Brush ForegroundOnMouseOver
        {
            get
            {
                var v = GetValue(ForegroundOnMouseOverProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnMouseOverProperty, value);
        }
        #endregion

        #region ForegroundOnMouseDown
        public static readonly DependencyProperty ForegroundOnMouseDownProperty = DependencyProperty.Register("ForegroundOnMouseDown", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Black));
        public Brush ForegroundOnMouseDown
        {
            get
            {
                var v = GetValue(ForegroundOnMouseDownProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnMouseDownProperty, value);
        }
        #endregion

        #region ForegroundOnDisabled
        public static readonly DependencyProperty ForegroundOnDisabledProperty = DependencyProperty.Register("ForegroundOnDisabled", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.DarkGray));
        public Brush ForegroundOnDisabled
        {
            get
            {
                var v = GetValue(ForegroundOnDisabledProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnDisabledProperty, value);
        }
        #endregion


        #region BackgroundOnMouseOver
        public static readonly DependencyProperty BackgroundOnMouseOverProperty = DependencyProperty.Register("BackgroundOnMouseOver", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Aqua));
        public Brush BackgroundOnMouseOver
        {
            get
            {
                var v = GetValue(BackgroundOnMouseOverProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnMouseOverProperty, value);
        }
        #endregion

        #region BackgroundOnMouseDown
        public static readonly DependencyProperty BackgroundOnMouseDownProperty = DependencyProperty.Register("BackgroundOnMouseDown", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Aquamarine));
        public Brush BackgroundOnMouseDown
        {
            get
            {
                var v = GetValue(BackgroundOnMouseDownProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnMouseDownProperty, value);
        }
        #endregion

        #region BackgroundOnDisabled
        public static readonly DependencyProperty BackgroundOnDisabledProperty = DependencyProperty.Register("BackgroundOnDisabled", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Gray));
        public Brush BackgroundOnDisabled
        {
            get
            {
                var v = GetValue(BackgroundOnDisabledProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnDisabledProperty, value);
        }
        #endregion


        #region OpacityOnMouseOver
        public static readonly DependencyProperty OpacityOnMouseOverProperty = DependencyProperty.Register("OpacityOnMouseOver", typeof(double), typeof(ButtonEx), new PropertyMetadata(1.0));
        public double OpacityOnMouseOver
        {
            get
            {
                var v = GetValue(OpacityOnMouseOverProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnMouseOverProperty, value);
        }
        #endregion

        #region OpacityOnMouseDown
        public static readonly DependencyProperty OpacityOnMouseDownProperty = DependencyProperty.Register("OpacityOnMouseDown", typeof(double), typeof(ButtonEx), new PropertyMetadata(0.5));
        public double OpacityOnMouseDown
        {
            get
            {
                var v = GetValue(OpacityOnMouseDownProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnMouseDownProperty, value);
        }
        #endregion

        #region OpacityOnDisabled
        public static readonly DependencyProperty OpacityOnDisabledProperty = DependencyProperty.Register("OpacityOnDisabled", typeof(double), typeof(ButtonEx), new PropertyMetadata(1.0));
        public double OpacityOnDisabled
        {
            get
            {
                var v = GetValue(OpacityOnDisabledProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnDisabledProperty, value);
        }
        #endregion


        #region BorderCornerRadius
        public static readonly DependencyProperty BorderCornerRadiusProperty = DependencyProperty.Register("BorderCornerRadius", typeof(CornerRadius), typeof(ButtonEx), new PropertyMetadata(new CornerRadius(0, 0, 0, 0)));
        public CornerRadius BorderCornerRadius
        {
            get
            {
                var v = GetValue(BorderCornerRadiusProperty);
                return (CornerRadius)v;
            }
            set => SetValue(BorderCornerRadiusProperty, value);
        }
        #endregion
    }
}
