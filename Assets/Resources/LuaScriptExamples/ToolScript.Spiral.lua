﻿Settings = {
    previewType="cube"
}

Widgets = {
    turns={label="Number of turns", type="float", min=0, max=20, default=6},
    steps={label="Number of steps per turn", type="int", min=1, max=32, default=12},
}

function OnTriggerReleased()
    points = {}
    totalSteps = turns * steps
    for i = 0, 1, 1/totalSteps do
        angle = math.pi * 2 * turns * i
        pos = {math.cos(angle) * i, math.sin(angle) * i, -(i * 2) + 1}
        rot = {0, angle * Mathf.Rad2Deg, 0}
        table.insert(points, {pos, rot})
    end
    return points
end