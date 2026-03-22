"""
TikTok video search using TikTokApi (David Teather).
Called from C# with arguments:
  --username "account_username"
  --keywords "politica,vladimir cerron,eleccion"  (comma-separated)
  --max-age-days 7
  --count 30
  --ms-token "token"  (optional)

Outputs JSON array of video objects to stdout.
"""
import asyncio
import argparse
import json
import sys
import os
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--username", required=True)
    parser.add_argument("--keywords", required=True, help="Comma-separated keywords")
    parser.add_argument("--max-age-days", type=int, default=7)
    parser.add_argument("--count", type=int, default=30)
    parser.add_argument("--ms-token", default=None)
    args = parser.parse_args()

    keywords = [k.strip() for k in args.keywords.split(",") if k.strip()]
    try:
        results = asyncio.run(search_videos(
            args.username, keywords, args.max_age_days, args.count, args.ms_token))
        print(json.dumps(results, ensure_ascii=False))
    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)


async def search_videos(username: str, keywords: list[str], max_age_days: int,
                        count: int, ms_token: str | None):
    from TikTokApi import TikTokApi

    tokens = [ms_token] if ms_token else [None]
    cutoff_ts = time.time() - (max_age_days * 86400)
    seen_ids = set()
    all_videos = []

    async with TikTokApi() as api:
        await api.create_sessions(
            ms_tokens=tokens, num_sessions=1, sleep_after=3,
            browser=os.getenv("TIKTOK_BROWSER", "chromium"),
        )

        search_url = "https://www.tiktok.com/api/search/item/full/"

        for keyword in keywords:
            cursor = 0
            keyword_collected = 0

            while keyword_collected < count:
                batch = min(20, count - keyword_collected)
                params = {
                    "keyword": keyword,
                    "count": batch,
                    "cursor": cursor,
                    "source": "search_video",
                }

                response = await api.make_request(url=search_url, params=params)
                item_list = response.get("item_list", [])
                if not item_list:
                    break

                for item in item_list:
                    author_id = item.get("author", {}).get("uniqueId", "")

                    # Filter by username if provided
                    if username and author_id.lower() != username.lower().lstrip("@"):
                        continue

                    video_id = item.get("id", "")
                    if video_id in seen_ids:
                        continue

                    # Filter by age
                    create_time = item.get("createTime", 0)
                    if isinstance(create_time, (int, float)) and create_time < cutoff_ts:
                        continue

                    seen_ids.add(video_id)
                    video_url = f"https://www.tiktok.com/@{author_id}/video/{video_id}"

                    upload_date = ""
                    if isinstance(create_time, (int, float)) and create_time > 0:
                        from datetime import datetime, timezone
                        dt = datetime.fromtimestamp(create_time, tz=timezone.utc)
                        upload_date = dt.strftime("%Y%m%d")

                    all_videos.append({
                        "id": video_id,
                        "title": item.get("desc", ""),
                        "uploader": author_id,
                        "webpage_url": video_url,
                        "upload_date": upload_date,
                        "timestamp": create_time,
                    })
                    keyword_collected += 1

                cursor = response.get("cursor", 0)
                if not response.get("has_more", False):
                    break

    return all_videos


if __name__ == "__main__":
    main()
