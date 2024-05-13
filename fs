---
# Tasks to manage filesystem mounts, sizes, fstypes, and swap
- name: Ensure the filesystems and swap spaces are correctly managed
  block:
    - name: Gather mounted filesystems and swap information
      ansible.builtin.setup:
        gather_subset:
          - "mounts"

    - name: Define list of managed sources from dictionary
      set_fact:
        managed_sources: "{{ nvs_linux_core_filesystem_mounts | dict2items | map(attribute='value.src') | list }}"

    - name: Remove unlisted filesystems and disable unneeded swaps
      block:
        - name: Unmount filesystem
          ansible.builtin.mount:
            path: "{{ item.mount }}"
            src: "{{ item.device }}"
            fstype: "{{ item.fstype }}"
            state: absent
          when: item.mount != 'none' and item.device not in managed_sources
        - name: Disable swap
          ansible.builtin.mount:
            path: none
            src: "{{ item.device }}"
            fstype: 'swap'
            state: absent
          when: item.mount == 'none' and item.device not in managed_sources and item.fstype == 'swap'
      loop: "{{ ansible_mounts }}"

    - name: Create or resize logical volumes for filesystems or swap
      community.general.lvol:
        vg: "{{ item.value.src | regex_replace('/dev/(.*)/.*', '\\1') }}"
        lv: "{{ item.value.src | regex_replace('/dev/.*/(.*)', '\\1') }}"
        size: "{{ item.value.size }}"
        resizefs: yes
        state: present
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.src is match("/dev/.*/.*") and item.value.fstype != 'swap'

    - name: Create or resize swap
      community.general.lvol:
        vg: "{{ item.value.src | regex_replace('/dev/(.*)/.*', '\\1') }}"
        lv: "{{ item.value.src | regex_replace('/dev/.*/(.*)', '\\1') }}"
        size: "{{ item.value.size }}"
        state: present
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.src is match("/dev/.*/.*") and item.value.fstype == 'swap'

    - name: Make and enable swap
      block:
        - name: Make swap area
          ansible.builtin.command:
            cmd: mkswap "{{ item.value.src }}"
        - name: Enable swap
          ansible.builtin.mount:
            path: none
            src: "{{ item.value.src }}"
            fstype: 'swap'
            opts: sw
            state: mounted
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.fstype == 'swap'

    - name: Create mount point for filesystems
      ansible.builtin.file:
        path: "{{ item.value.path }}"
        state: directory
        owner: root
        group: root
        mode: '0755'
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.fstype != 'swap'

    - name: Mount the filesystem
      ansible.builtin.mount:
        path: "{{ item.value.path }}"
        src: "{{ item.value.src }}"
        fstype: "{{ item.value.fstype }}"
        opts: defaults
        state: mounted
      loop: "{{ nvs_linux_core_filesystem_mounts | dict2items }}"
      when: item.value.fstype != 'swap'
      notify: Mount changes

  when: nvs_linux_core_filesystem_mounts is defined
