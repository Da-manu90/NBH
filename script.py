import re
import sys

with open(r'c:\Data\NBH\RatingPage.xaml', 'r', encoding='utf-8') as f:
    text = f.read()

# Replace Chat Rating Frame
chat_pattern = re.compile(r'<!-- Frame mit Sternen => nur sichtbar, wenn HasChatRating=true -->\s*<Frame BorderColor="Black".*?</Frame>', re.DOTALL)
chat_repl = r'''<Border Padding="10" IsVisible="{Binding HasChatRating}">
                <Border.StrokeShape><RoundRectangle CornerRadius="10"/></Border.StrokeShape>
                <Border.Stroke><SolidColorBrush Color="Black"/></Border.Stroke>
                <Border.Background><SolidColorBrush Color="{AppThemeBinding Light=White, Dark=#1e1e1e}"/></Border.Background>
                <Grid RowDefinitions="Auto,Auto,Auto" ColumnDefinitions="Auto,Auto" Padding="5" RowSpacing="10" ColumnSpacing="10">
                    <Label Grid.Row="0" Grid.Column="0" Text="Hilfsbereitschaft:" FontSize="18" FontAttributes="Bold" TextColor="{AppThemeBinding Light=Black, Dark=White}" VerticalOptions="Center"/>
                    <controls:StarRatingControl Grid.Row="0" Grid.Column="1" Rating="{Binding ChatHelpfulnessAverage}" IsInteractive="False" HeightRequest="32"/>
                    
                    <Label Grid.Row="1" Grid.Column="0" Text="Freundlichkeit:" FontSize="18" FontAttributes="Bold" TextColor="{AppThemeBinding Light=Black, Dark=White}" VerticalOptions="Center"/>
                    <controls:StarRatingControl Grid.Row="1" Grid.Column="1" Rating="{Binding ChatFriendlinessAverage}" IsInteractive="False" HeightRequest="32"/>
                    
                    <Label Grid.Row="2" Grid.Column="0" Text="Antwortzeit:" FontSize="18" FontAttributes="Bold" TextColor="{AppThemeBinding Light=Black, Dark=White}" VerticalOptions="Center"/>
                    <controls:StarRatingControl Grid.Row="2" Grid.Column="1" Rating="{Binding ChatResponseTimeAverage}" IsInteractive="False" HeightRequest="32"/>
                </Grid>
            </Border>'''

# Replace Event Rating Frame
event_pattern = re.compile(r'<!-- Frame mit Sternen => nur sichtbar, wenn HasEventRating=true -->\s*<Frame BorderColor="Black".*?</Frame>', re.DOTALL)
event_repl = r'''<Border Padding="10" IsVisible="{Binding HasEventRating}">
                <Border.StrokeShape><RoundRectangle CornerRadius="10"/></Border.StrokeShape>
                <Border.Stroke><SolidColorBrush Color="Black"/></Border.Stroke>
                <Border.Background><SolidColorBrush Color="{AppThemeBinding Light=White, Dark=#1e1e1e}"/></Border.Background>
                <Grid RowDefinitions="Auto,Auto,Auto" ColumnDefinitions="Auto,Auto" Padding="5" RowSpacing="10" ColumnSpacing="10">
                    <Label Grid.Row="0" Grid.Column="0" Text="Freundlichkeit:" FontSize="18" FontAttributes="Bold" TextColor="{AppThemeBinding Light=Black, Dark=White}" VerticalOptions="Center"/>
                    <controls:StarRatingControl Grid.Row="0" Grid.Column="1" Rating="{Binding EventFriendlinessAverage}" IsInteractive="False" HeightRequest="32"/>
                    
                    <Label Grid.Row="1" Grid.Column="0" Text="Beschreibung&#x0a;des Events:" FontSize="18" FontAttributes="Bold" TextColor="{AppThemeBinding Light=Black, Dark=White}" LineBreakMode="WordWrap" VerticalOptions="Center"/>
                    <controls:StarRatingControl Grid.Row="1" Grid.Column="1" Rating="{Binding EventDescriptionAccuracyAverage}" IsInteractive="False" HeightRequest="32"/>
                    
                    <Label Grid.Row="2" Grid.Column="0" Text="Zuverlässigkeit:" FontSize="18" FontAttributes="Bold" TextColor="{AppThemeBinding Light=Black, Dark=White}" VerticalOptions="Center"/>
                    <controls:StarRatingControl Grid.Row="2" Grid.Column="1" Rating="{Binding EventReliabilityAverage}" IsInteractive="False" HeightRequest="32"/>
                </Grid>
            </Border>'''

# Replace Comment CollectionView Frame
cv_pattern = re.compile(r'<Frame BorderColor="Gray"\s*CornerRadius="30"\s*Padding="0"\s*Margin="0,0,0,10"\s*HasShadow="False">', re.DOTALL)
cv_repl = r'''<Border Stroke="Gray" StrokeThickness="1" Margin="0,0,0,10" Padding="0">
                            <Border.StrokeShape><RoundRectangle CornerRadius="30"/></Border.StrokeShape>
                            <Border.Background><SolidColorBrush Color="Transparent"/></Border.Background>'''

text = re.sub(chat_pattern, chat_repl, text)
text = re.sub(event_pattern, event_repl, text)

# For CV, we also need to close it
text = re.sub(cv_pattern, cv_repl, text)
text = text.replace('</Frame>', '</Border>')

with open(r'c:\Data\NBH\RatingPage.xaml', 'w', encoding='utf-8') as f:
    f.write(text)

print("Replacement done.")
