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
        <Setter Property="Background" Value="{DynamicResource MainColor1Brush}"/>
        <Setter Property="Foreground" Value="{DynamicResource MainColor1ForegroundBrush}"/>
        <Setter Property="BackgroundOnMouseOver" Value="{DynamicResource MainColor1LighterBrush}"/>
        <Setter Property="ForegroundOnMouseOver" Value="{DynamicResource MainColor1ForegroundBrush}"/>
        <Setter Property="OpacityOnMouseOver" Value="1"/>
        <Setter Property="BackgroundOnPressed" Value="{DynamicResource MainColor1LighterBrush}"/>
        <Setter Property="ForegroundOnPressed" Value="{DynamicResource MainColor1ForegroundBrush}"/>
        <Setter Property="OpacityOnPressed" Value="0.5"/>
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
                                <Setter TargetName="ButtonBorder" Property="Background" Value="{Binding BackgroundOnPressed, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                                <Setter TargetName="ButtonBorder" Property="Opacity" Value="{Binding OpacityOnPressed, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
                                <Setter TargetName="ButtonContentPresenter" Property="TextBlock.Foreground" Value="{Binding ForegroundOnPressed, RelativeSource={x:Static RelativeSource.TemplatedParent}}"/>
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
                    <Setter Property="Foreground" Value="{Binding ForegroundOnPressed, RelativeSource={x:Static RelativeSource.Self}}"/>
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

        #region ForegroundOnPressed
        public static readonly DependencyProperty ForegroundOnPressedProperty = DependencyProperty.Register("ForegroundOnPressed", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Black));
        public Brush ForegroundOnPressed
        {
            get
            {
                var v = GetValue(ForegroundOnPressedProperty);
                return (Brush)v;
            }
            set => SetValue(ForegroundOnPressedProperty, value);
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

        #region BackgroundOnPressed
        public static readonly DependencyProperty BackgroundOnPressedProperty = DependencyProperty.Register("BackgroundOnPressed", typeof(Brush), typeof(ButtonEx), new PropertyMetadata(Brushes.Aquamarine));
        public Brush BackgroundOnPressed
        {
            get
            {
                var v = GetValue(BackgroundOnPressedProperty);
                return (Brush)v;
            }
            set => SetValue(BackgroundOnPressedProperty, value);
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

        #region OpacityOnPressed
        public static readonly DependencyProperty OpacityOnPressedProperty = DependencyProperty.Register("OpacityOnPressed", typeof(double), typeof(ButtonEx), new PropertyMetadata(0.5));
        public double OpacityOnPressed
        {
            get
            {
                var v = GetValue(OpacityOnPressedProperty);
                return (double)v;
            }
            set => SetValue(OpacityOnPressedProperty, value);
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
