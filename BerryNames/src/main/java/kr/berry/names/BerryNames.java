package kr.berry.names;

import net.kyori.adventure.text.Component;
import net.kyori.adventure.text.format.NamedTextColor;
import org.bukkit.Color;
import org.bukkit.command.Command;
import org.bukkit.command.CommandSender;
import org.bukkit.command.TabCompleter;
import org.bukkit.configuration.file.YamlConfiguration;
import org.bukkit.entity.Display;
import org.bukkit.entity.Player;
import org.bukkit.entity.TextDisplay;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.player.PlayerJoinEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.plugin.java.JavaPlugin;
import org.bukkit.scoreboard.Team;
import org.bukkit.util.Transformation;
import org.joml.AxisAngle4f;
import org.joml.Vector3f;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.Map;
import java.util.List;
import java.util.UUID;

public final class BerryNames extends JavaPlugin implements Listener, TabCompleter {
    private static final String PREFIX = "베리";
    private static final String[] DIGITS = {"", "일", "이", "삼", "사", "오", "육", "칠", "팔", "구"};

    private File dataFile;
    private YamlConfiguration data;
    private final Map<UUID, TextDisplay> tags = new HashMap<>();
    private Team hideTeam;

    @Override
    public void onEnable() {
        getDataFolder().mkdirs();
        dataFile = new File(getDataFolder(), "players.yml");
        data = YamlConfiguration.loadConfiguration(dataFile);
        getServer().getPluginManager().registerEvents(this, this);
        getCommand("berrynames").setTabCompleter(this);
        var board = getServer().getScoreboardManager().getMainScoreboard();
        hideTeam = board.getTeam("berrynames_hide");
        if (hideTeam == null) hideTeam = board.registerNewTeam("berrynames_hide");
        hideTeam.setOption(Team.Option.NAME_TAG_VISIBILITY, Team.OptionStatus.NEVER);
        // 텔레포트/월드 이동으로 떨어진 이름표를 복구한다.
        getServer().getScheduler().runTaskTimer(this, () -> {
            for (Player p : getServer().getOnlinePlayers()) ensureTag(p);
        }, 10L, 10L);
        for (Player p : getServer().getOnlinePlayers()) {
            if (data.getString("real." + p.getUniqueId()) == null) data.set("real." + p.getUniqueId(), p.getName());
            apply(p);
        }
    }

    @Override
    public void onDisable() {
        for (TextDisplay d : tags.values()) d.remove();
        tags.clear();
        if (hideTeam != null) {
            try { hideTeam.unregister(); } catch (IllegalStateException ignored) { }
        }
    }

    /** 처음 들어온 순서대로 번호를 부여하고 저장한다. 재접속해도 번호는 유지된다. */
    private synchronized int numberOf(UUID id) {
        String key = "players." + id;
        int n = data.getInt(key, 0);
        if (n == 0) {
            n = data.getInt("next", 1);
            data.set(key, n);
            data.set("next", n + 1);
            try {
                data.save(dataFile);
            } catch (IOException e) {
                getLogger().severe("players.yml 저장 실패: " + e.getMessage());
            }
        }
        return n;
    }

    private String nameOf(UUID id) {
        String custom = data.getString("custom." + id);
        if (custom != null) return custom;
        String name = PREFIX + toKorean(numberOf(id));
        return name.length() > 16 ? name.substring(0, 16) : name;
    }

    private void save() {
        try {
            data.save(dataFile);
        } catch (IOException e) {
            getLogger().severe("players.yml 저장 실패: " + e.getMessage());
        }
    }

    /** 현재 표시 이름(또는 온라인 접속 이름)으로 UUID를 찾는다. 오프라인도 가능. */
    private UUID findByName(String name) {
        Player online = getServer().getPlayerExact(name);
        if (online != null) return online.getUniqueId();
        var sec = data.getConfigurationSection("players");
        if (sec == null) return null;
        for (String key : sec.getKeys(false)) {
            UUID id = UUID.fromString(key);
            if (nameOf(id).equalsIgnoreCase(name)) return id;
        }
        for (String key : sec.getKeys(false)) {
            if (name.equalsIgnoreCase(data.getString("real." + key))) return UUID.fromString(key);
        }
        return null;
    }

    private boolean nameTaken(String name, UUID except) {
        var sec = data.getConfigurationSection("players");
        if (sec == null) return false;
        for (String key : sec.getKeys(false)) {
            UUID id = UUID.fromString(key);
            if (!id.equals(except) && nameOf(id).equalsIgnoreCase(name)) return true;
        }
        return false;
    }

    @EventHandler(priority = EventPriority.LOWEST)
    public void onJoin(PlayerJoinEvent e) {
        Player p = e.getPlayer();
        // 접속 직후의 이름이 실제 닉네임이다.
        data.set("real." + p.getUniqueId(), p.getName());
        save();
        apply(p);
        e.joinMessage(Component.text(nameOf(p.getUniqueId()) + "님이 게임에 참여했습니다.", NamedTextColor.YELLOW));
    }

    @EventHandler(priority = EventPriority.LOWEST)
    public void onQuit(PlayerQuitEvent e) {
        TextDisplay d = tags.remove(e.getPlayer().getUniqueId());
        if (d != null) d.remove();
        hideTeam.removeEntry(e.getPlayer().getName());
        e.quitMessage(Component.text(nameOf(e.getPlayer().getUniqueId()) + "님이 게임을 나갔습니다.", NamedTextColor.YELLOW));
    }

    private void apply(Player p) {
        String name = nameOf(p.getUniqueId());
        // 프로필 이름은 ASCII만 허용되어 한글로 바꿀 수 없으므로, 머리 위 이름은 별도 텍스트로 띄운다.
        hideTeam.addEntry(p.getName());
        applyNames(p, name);
        ensureTag(p);
    }

    private void ensureTag(Player p) {
        String name = nameOf(p.getUniqueId());
        TextDisplay d = tags.get(p.getUniqueId());
        if (d == null || !d.isValid() || !p.getPassengers().contains(d)) {
            if (d != null) d.remove();
            d = p.getWorld().spawn(p.getLocation(), TextDisplay.class, t -> {
                t.setPersistent(false);
                t.setBillboard(Display.Billboard.CENTER);
                t.setBackgroundColor(Color.fromARGB(64, 0, 0, 0));
                t.setTransformation(new Transformation(new Vector3f(0, 0.35f, 0), new AxisAngle4f(), new Vector3f(1, 1, 1), new AxisAngle4f()));
            });
            p.addPassenger(d);
            p.hideEntity(this, d); // 본인에게는 보이지 않게
            tags.put(p.getUniqueId(), d);
        }
        d.text(Component.text(name));
    }

    private void applyNames(Player p, String name) {
        Component c = Component.text(name);
        p.displayName(c);
        p.playerListName(c);
    }

    /** 1 -> 일, 10 -> 십, 21 -> 이십일, 100 -> 백, 12345 -> 만이천삼백사십오 */
    static String toKorean(int n) {
        if (n <= 0) return "영";
        StringBuilder sb = new StringBuilder();
        if (n >= 10000) {
            int man = n / 10000;
            sb.append(man == 1 ? "" : toKorean(man)).append("만");
            n %= 10000;
        }
        String[] unit = {"천", "백", "십", ""};
        int[] div = {1000, 100, 10, 1};
        for (int i = 0; i < 4; i++) {
            int d = n / div[i];
            n %= div[i];
            if (d == 0) continue;
            if (d == 1 && i < 3) sb.append(unit[i]);
            else sb.append(DIGITS[d]).append(unit[i]);
        }
        return sb.toString();
    }

    @Override
    public boolean onCommand(CommandSender s, Command c, String l, String[] a) {
        String sub = a.length > 0 ? a[0].toLowerCase() : "info";
        switch (sub) {
            case "rename" -> {
                if (a.length < 3) { s.sendMessage("사용법: /berrynames rename <현재이름> <새이름>"); return true; }
                UUID id = findByName(a[1]);
                if (id == null) { s.sendMessage("그런 이용자를 찾을 수 없습니다: " + a[1]); return true; }
                String name = a[2];
                if (name.length() > 16 || !name.matches("[0-9A-Za-z_가-힣]+")) {
                    s.sendMessage("이름은 16자 이하의 한글/영문/숫자/_ 만 가능합니다."); return true;
                }
                if (nameTaken(name, id)) { s.sendMessage("이미 사용 중인 이름입니다."); return true; }
                data.set("custom." + id, name);
                save();
                Player p = getServer().getPlayer(id);
                if (p != null) apply(p);
                s.sendMessage(a[1] + " → " + name + (p == null ? " (오프라인: 다음 접속 때 적용)" : ""));
            }
            case "reset" -> {
                if (a.length < 2) { s.sendMessage("사용법: /berrynames reset <이름>"); return true; }
                UUID id = findByName(a[1]);
                if (id == null) { s.sendMessage("그런 이용자를 찾을 수 없습니다: " + a[1]); return true; }
                data.set("custom." + id, null);
                save();
                Player p = getServer().getPlayer(id);
                if (p != null) apply(p);
                s.sendMessage("순번 이름으로 되돌렸습니다: " + nameOf(id));
            }
            case "whois" -> {
                if (a.length < 2) {
                    s.sendMessage("접속 중인 이용자:");
                    for (Player p : getServer().getOnlinePlayers())
                        s.sendMessage(" " + nameOf(p.getUniqueId()) + " = " + data.getString("real." + p.getUniqueId(), "?"));
                    return true;
                }
                UUID id = findByName(a[1]);
                if (id == null) { s.sendMessage("그런 이용자를 찾을 수 없습니다: " + a[1]); return true; }
                s.sendMessage(nameOf(id) + " = 실제 닉네임 " + data.getString("real." + id, "?")
                        + " (번호 " + data.getInt("players." + id) + ", " + (getServer().getPlayer(id) != null ? "온라인" : "오프라인") + ")");
                s.sendMessage("UUID: " + id);
            }
            case "reload" -> {
                data = YamlConfiguration.loadConfiguration(dataFile);
                s.sendMessage("데이터를 다시 불러왔습니다.");
            }
            default -> {
                int next = data.getInt("next", 1);
                s.sendMessage("다음 번호: " + next + " (" + PREFIX + toKorean(next) + ")");
                s.sendMessage("/berrynames rename <이름> <새이름> | reset <이름> | whois [이름] | reload");
            }
        }
        return true;
    }

    @Override
    public List<String> onTabComplete(CommandSender s, Command c, String l, String[] a) {
        List<String> out = new ArrayList<>();
        if (a.length == 1) {
            for (String x : List.of("rename", "reset", "whois", "reload", "info")) if (x.startsWith(a[0].toLowerCase())) out.add(x);
        } else if (a.length == 2 && (a[0].equalsIgnoreCase("rename") || a[0].equalsIgnoreCase("reset") || a[0].equalsIgnoreCase("whois"))) {
            for (Player p : getServer().getOnlinePlayers()) {
                String n = nameOf(p.getUniqueId());
                if (n.toLowerCase().startsWith(a[1].toLowerCase())) out.add(n);
            }
        }
        return out;
    }
}
