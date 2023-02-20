﻿Settings = {
    space="widget"
}

Widgets = {
    copies={label="Number of copies", type="int", min=0, max=36, default=4},
}

function Main()

    if brush.triggerIsPressedThisFrame then
        symmetry.setTransform(brush.position, brush.rotation)
    end

    -- Don't allow painting immediately otherwise you get stray lines
    if brush.triggerIsPressed and brush.timeSincePressed > .05 then
        brush.forcePaintingOff(false)
    else
        brush.forcePaintingOff(true)
    end

    pointers = {}
    Colors = {}
    theta = 360.0 / copies

    for i = 0, copies - 1 do
        table.insert(pointers, {position={0, 0, 0}, rotation={0, i * theta, 0}})
    end

    return pointers
end