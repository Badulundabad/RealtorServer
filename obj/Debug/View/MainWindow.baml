   M S B A M L   `   `   `  ����H  DRealtorServer, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null-clr-namespace:RealtorServerRealtorServer  5   	   A%clr-namespace:RealtorServer.ViewModelRealtorServer.ViewModel  5   	   #    RealtorServer.View.MainWindow   5   	   \ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    5   	   R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   -localclr-namespace:RealtorServer     5   	   4vm%clr-namespace:RealtorServer.ViewModel     5   	     � Title$  RealtyServer��5	   	   $	��400��6   $	��400��6+    � 	Resources 5
         LogItem_Style � ColumnDefinitions  UserItem_Style  ClientItem_Style �� TextAlignment  ButtonStyle �� 
TargetType �� Property �� Value �� Triggers �� Binding	 �� Value��%�  &	        &	 �     &	 ;    &	 g    �� 5   
   .R�5      �  5      �� 5      $��95 �6'   �� 5      $��35��6'   �� 5      .+�5      �� ��6   �� *
DateTime+�� 5      $��2��5      ��5      �� *Text+5      5      5      �� 5   
   .R�5      �  5      �� 5      $	��120 �6'   �� 5      $	��120 �6'   �� 5      $��40 �6'   .+�5      �� ��6   �� *UserId+�� 5      $��1��5       ��5      �� *Name+5       �� 5!      $��2��5"      ��5!      �� *
Password+5"      5#      5$      �� 5%   
   .R�5&      � 
������� 5'      �� 5(      �� 5)      .+�5+      �� $ Center=�62   $	��185��6I   $��20(�6U   ��6   �� *Name+6U   �� 5,      $ Center=�67   $	��185��6N   $��20(�6Z   $��1��6h   ��6   �� *	IpAddress+6h   5-      5.      �� 5/   
   " ��6$   .�50      ��  � ��$	 270��6&   �� 51       � ��$ 50��6'   �� 52       � ��$ 2��6)   �� 53       � ��$ 2��60   �� 54       � ��$ 0q�60    55      �� 56       6   �� *	IsRunning+		 false6<   .a�57      ��  � ��$	 Run��60   �� 58       � ��
 � � �63   �� 59       � �� 60   �� *
RunCommand+5:      �� 5;       6   �� *	IsRunning+	 true6<   .a�5<      ��  � ��$
 Stop��60   �� 5=       � ��
 �  ��63   �� 5>       � �� 60   �� *StopCommand+5?      5A      5B      .��5C      � 
�������
 � RowDefinitions
 5D   
   �� 5E      $	��300 �6   �� 5F      .+�5H   
   �� -   5         ��Name$ OnlineClients��5H      #��[ 5I      ��5J      �� *Server.Clients+ �� UpdateSourceTrigger$ PropertyChanged=��� 5K   
   #��[ 6   5L      5M      