$JSON = Get-Content "C:\temp\de.json"  -encoding UTF8 | ConvertFrom-Json 
[xml] $XML = Get-Content "C:\temp\de.xaml"  -encoding UTF8 

for ($i =0;$i -lt $XML.ResourceDictionary.String.Count; $i++)
{
    $JSONMEMBER = Get-Member -InputObject $JSON -Name $XML.ResourceDictionary.String[$i].Key
    if ($JSONMEMBER -ne $null)
    {
        $NewValue = $JSONMEMBER.Definition.Split("=")[1]
        if ($NewValue -ne $null)
        {
            $XML.ResourceDictionary.String[$i].'#text' = $NewValue
        }
        else
        {
            Write-Warning ("Value is empty " + $i)
            Write-Warning -Message $XML.ResourceDictionary.String[$i].Key
            pause
        }
    }
    else
    {
        Write-Warning ("JSONMEMBER is empty " + $i)
        Write-Warning -Message $XML.ResourceDictionary.String[$i].Key
        pause
    }
}
$XML.Save("C:\temp\de2.xaml")