create a project here called "wetter" 

It should provide a single page web-frontend that i can use on a tablet, fullscreen in portrait mode (9 by 16). 
The tablet uses a OLED display. 

The website should provide a light and dark mode and theme that adjust according to the current day-light situation according to the current server system time. 

The main page should be split in 3 segments vertically. 

First segment: A clock display with Hours, Minutes and Seconds in digits and an analog clock next to it. 

Second segment: Weather forecast and current weather for a location i can configure in appsettings.json

Third segment: a random quote that is updated once every minute. 


add a fullscreen button to toggle fullscreen on the tablet i am seeing this on. 
ensure the display is showing day names in german. 
make the language configurable in appsettings.json. 
ensure text and elements on the page are large enough to read from further away. the 10" tablet will be mounted on the wall, and i want to be able to read it from across the room. 
include a functionality that automatically reloads the page, if the server is restarted. 
ensure the weather is automatically updated every 10 minutes without reloading the page. 
ensure the content is not leaking outside the screen and it is all visible on screen. 
instead of showing the "Weather-Code" as integer, show it as a nice icon. 
instead of bland white and black background, let's show a sky reflecting the current weather situation instead. ensure it has clouds and blue sky or starry night and so on according the current weather. make sure we can still read all the text and see the icons (contrast). 
add a button to reload the page opposite of the fullscreen button. 
