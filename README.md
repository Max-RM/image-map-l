Using the -l or --log option, you can record the ID of Map Arts sent to your minecraft worlds in a file 
"ImageMapCMD_logged_IDs.txt" which will appear in the same folder along with ImageMap-cmd.exe after the process is completed.

This option should be used at the end of the command, for example:

```ImageMap-cmd.exe <path to the Minecrfat world folder> -- import <path to the single image or folder with a lot of images> -l (or --log)```
At the end of the process, the contents of the file "ImageMapCMD_logged_IDs.txt" will look something like this:
```
<FirstID>
<SecondID>
done
```
For example user imported 5 images into the world, but there were already 167 images in this world.
Then log file will contain such text:
```
168
172
done
```
