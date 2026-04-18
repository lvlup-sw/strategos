import { z } from "zod"

export const TeamSpawnedDataSchema = z.object({ "teamSize": z.number().int().gte(-2147483648).lte(2147483647), "teammateNames": z.array(z.string()), "taskCount": z.number().int().gte(-2147483648).lte(2147483647), "dispatchMode": z.string() }).describe("Data payload for `team.spawned` events.")
export type TeamSpawnedDataSchema = z.infer<typeof TeamSpawnedDataSchema>
