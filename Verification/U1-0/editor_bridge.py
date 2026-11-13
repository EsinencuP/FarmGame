"""Call the existing local Unity MCP; no Play Mode or tests are started here."""
import argparse
import asyncio
import json
from pathlib import Path
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client


async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--code", type=Path)
    parser.add_argument("--tool")
    parser.add_argument("--args", default="{}")
    parser.add_argument("--resource")
    parser.add_argument("--out", type=Path)
    args = parser.parse_args()
    async with streamablehttp_client("http://127.0.0.1:8080/mcp") as (reader, writer, _):
        async with ClientSession(reader, writer) as session:
            await session.initialize()
            await session.call_tool("set_active_instance", {"instance": "FarmGame@60d241d85605540d"})
            if args.resource:
                result = await session.read_resource(args.resource)
            elif args.code:
                result = await session.call_tool("execute_code", {"action": "execute", "code": args.code.read_text(encoding="utf-8-sig")})
            else:
                result = await session.call_tool(args.tool, json.loads(args.args))
            data = result.model_dump_json(indent=2)
            if args.out:
                args.out.parent.mkdir(parents=True, exist_ok=True)
                args.out.write_text(data, encoding="utf-8")
            print(json.dumps(getattr(result, "structuredContent", None) or result.model_dump(mode="json"), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    asyncio.run(main())
